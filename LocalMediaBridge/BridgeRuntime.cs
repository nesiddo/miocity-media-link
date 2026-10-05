using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;

namespace MioCity.LocalMediaBridge;

/// <summary>
/// A loopback-only WebSocket server for the FiveM NUI. No HTTP route exposes
/// media data; paired clients must present the randomly generated key.
/// </summary>
public sealed class BridgeRuntime : IAsyncDisposable
{
    public const int Port = 18765;
    // One FiveM NUI normally holds a single socket. The caps only stop a
    // misbehaving local page from opening an unbounded number of sockets.
    private const int MaximumNuiClients = 4;
    private const int MaximumMapViewers = 8;
    private const int MaximumMessageBytes = 64 * 1024;   // a full blip list (up to 500 entries) fits
    private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);
    private static readonly Regex AllowedNuiOrigin = new(
        @"^https://cfx-nui-[A-Za-z0-9_-]+(?::\d+)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TrustedZsxNuiOrigin = new(
        @"^https://cfx-nui-zsx_uiv2(?::\d+)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private const string DefaultContentSecurityPolicy =
        "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    private readonly ConcurrentDictionary<Guid, BridgeClient> _clients = new();
    private readonly ConcurrentDictionary<Guid, BridgeClient> _mapViewers = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly MediaSessionService _media;
    private readonly AudioSpectrumService _spectrum = new();
    // app 1.8.0: a YouTube live chat the game asked for (youtube.watch), relayed to the game's UI
    private readonly YouTubeChatService _youtube = new();
    private readonly object _mediaBroadcastGate = new();
    private WebApplication? _application;
    private bool _started;
    private int _disposeStarted;
    private string _lastBroadcastArtwork = string.Empty;
    private string _lastBroadcastMediaKey = string.Empty;
    private CompanionMapState _mapState = CompanionMapState.Empty;
    private const int MaximumBlips = 500;
    private object _mapBlips = new { blips = Array.Empty<double[]>() };
    // app 1.4.0: what the game lets the map offer, public-service members and dispatch calls (on-duty police / EMS)
    private const int MaximumUnits = 150;
    private const int MaximumAlerts = 20;
    private const int MaximumViewerMessageBytes = 1024;
    private static readonly TimeSpan ViewerCommandInterval = TimeSpan.FromMilliseconds(300);
    private CompanionMapConfig _mapConfig = CompanionMapConfig.Empty;
    private object _mapUnits = new { units = Array.Empty<object[]>() };
    private object _mapAlerts = new { alerts = Array.Empty<CompanionMapAlert>() };

    public BridgeRuntime(BridgeSettings settings)
    {
        Settings = settings;
        // The one-second timeline refresh is useful only while a connected
        // player has enabled the media widget. Otherwise SMTC events alone
        // keep the cached state current and idle CPU work stays near zero.
        _media = new MediaSessionService(() => _clients.Values.Any(client => client.MediaEnabled));
        _media.StateChanged += PublishMediaAsync;
        _spectrum.SpectrumChanged += PublishSpectrumAsync;
        _youtube.StatusChanged += status => BroadcastAsync(_clients.Values, "youtube.status", status);
        _youtube.MessagesReceived += messages => BroadcastAsync(_clients.Values, "youtube.chat", new { messages });
    }

    public BridgeSettings Settings { get; }
    public bool IsStarted => _started;
    public string Status => _started ? _media.Status : "Bridgeを起動しています";
    public string SpectrumStatus => _spectrum.Status;
    public int ConnectedClientCount => _clients.Count;
    public int MapViewerCount => _mapViewers.Count;

    public async Task StartAsync()
    {
        if (_started) return;
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            // Do not change this to IPAddress.Any / 0.0.0.0. The bridge is
            // intentionally usable only from the same Windows user session.
            options.Listen(IPAddress.Loopback, Port, listen => listen.Protocols = HttpProtocols.Http1);
            options.AddServerHeader = false;
            options.Limits.MaxRequestHeadersTotalSize = 16 * 1024;
        });
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            // Reject DNS-rebinding style requests whose Host is not the
            // loopback name this app actually serves.
            if (!IsAllowedHost(context.Request.Host))
            {
                context.Response.StatusCode = StatusCodes.Status421MisdirectedRequest;
                return;
            }
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers.CacheControl = "no-store";
            headers.ContentSecurityPolicy = DefaultContentSecurityPolicy;
            await next(context);
        });
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
        app.MapGet("/", () => Results.Content(CreateLandingPage(), "text/html; charset=utf-8"));
        app.MapGet("/health", () => Results.Json(new { running = true, protocol = 1, map = 2 }));
        app.MapGet("/map", HandleMapPage);
        app.MapGet("/blip/{sprite:int}.png", HandleBlipIconAsync);
        app.MapGet("/blip/names.json", HandleBlipNames);
        app.Map("/map-view", HandleMapViewerAsync);
        app.Map("/bridge", HandleBridgeAsync);

        _application = app;
        await app.StartAsync(_shutdown.Token);
        _started = true;
        await _media.StartAsync(_shutdown.Token);
    }

    private static bool IsAllowedHost(HostString host)
        => host.Port == Port &&
           (string.Equals(host.Host, "127.0.0.1", StringComparison.Ordinal) ||
            string.Equals(host.Host, "localhost", StringComparison.OrdinalIgnoreCase));

    private IResult HandleMapPage(HttpContext context)
    {
        if (!MatchesMapViewerToken(context.Request.Query["token"])) return Results.Unauthorized();
        context.Response.Headers.ContentSecurityPolicy = CompanionMapPage.ContentSecurityPolicy;
        return Results.Content(CompanionMapPage.Create(), "text/html; charset=utf-8");
    }

    private static async Task HandleBlipIconAsync(HttpContext context, int sprite)
    {
        // only the map page (same origin) may use them; other sites must not be able to probe for this app
        context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        var bytes = BlipIcons.IsKnown(sprite) ? await BlipIcons.GetAsync(sprite, context.RequestAborted) : null;
        if (bytes is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        // icons never change: let the map page's browser keep them
        context.Response.Headers.CacheControl = "public, max-age=604800, immutable";
        context.Response.ContentType = "image/png";
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }

    private static IResult HandleBlipNames(HttpContext context)
    {
        context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        context.Response.Headers.CacheControl = "public, max-age=86400";
        return Results.Json(BlipIcons.AllNames.ToDictionary(pair => pair.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), pair => pair.Value));
    }

    private async Task HandleBridgeAsync(HttpContext context)
    {
        if (!IPAddress.IsLoopback(context.Connection.RemoteIpAddress ?? IPAddress.None))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (!context.WebSockets.IsWebSocketRequest || !IsAllowedNuiOrigin(context.Request.Headers.Origin.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (_clients.Count >= MaximumNuiClients)
        {
            // Not 1008: the NUI treats a policy close as a stale pairing key.
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        var origin = context.Request.Headers.Origin.ToString();
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var buffer = new byte[MaximumMessageBytes];
        ClientEnvelope? envelope;
        using (var helloTimeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted))
        {
            // An unauthenticated socket must say hello promptly.
            helloTimeout.CancelAfter(HelloTimeout);
            try { envelope = await ReceiveEnvelopeAsync(socket, buffer, helloTimeout.Token); }
            catch (Exception exception) when (exception is OperationCanceledException or WebSocketException) { return; }
        }
        var automaticPairing = envelope?.RequestPairing == true && IsTrustedZsxNuiOrigin(origin);
        if (envelope is null || envelope.Type != "hello" || envelope.Protocol != 1 ||
            (!MatchesPairingToken(envelope.Token) && !automaticPairing))
        {
            await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Pairing failed", context.RequestAborted);
            return;
        }

        var id = Guid.NewGuid();
        var client = new BridgeClient { Socket = socket };
        _clients[id] = client;
        try
        {
            if (automaticPairing)
            {
                await SendAsync(client, "bridge.pairing", new
                {
                    token = Settings.PairingToken,
                    automatic = true,
                }, context.RequestAborted);
            }
            await SendAsync(client, "bridge.status", new
            {
                connected = true,
                message = Status,
                gameConnected = true,
                autoSuspend = true,
                mapUrl = $"http://127.0.0.1:{Port}/map?token={Settings.MapViewerToken}",
            }, context.RequestAborted);
            await SendAsync(client, "media.state", _media.CurrentState, context.RequestAborted);
            // the game collects map data (position, blips) only while a map page is open
            await SendAsync(client, "map.viewers", new { count = _mapViewers.Count }, context.RequestAborted);
            await SendAsync(client, "youtube.status", _youtube.Status, context.RequestAborted);

            while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
            {
                var message = await ReceiveEnvelopeAsync(socket, buffer, context.RequestAborted);
                if (message is null) break;
                await HandleClientMessageAsync(client, message, context.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal browser/page shutdown.
        }
        catch (WebSocketException)
        {
            // The game UI may disappear while a send is pending.
        }
        finally
        {
            // SendLock is intentionally not disposed: a concurrent broadcast
            // may still be waiting on it, and SemaphoreSlim owns no handle
            // unless AvailableWaitHandle is used.
            _clients.TryRemove(id, out _);
            RefreshOptionalServices();
            if (_clients.IsEmpty)
            {
                await BroadcastMapStatusAsync(false);
                // the game closed: stop reading YouTube until it asks again
                _youtube.Watch(string.Empty);
            }
        }
    }

    private async Task HandleMapViewerAsync(HttpContext context)
    {
        if (!IPAddress.IsLoopback(context.Connection.RemoteIpAddress ?? IPAddress.None) ||
            !context.WebSockets.IsWebSocketRequest ||
            !IsAllowedMapOrigin(context.Request.Headers.Origin.ToString()) ||
            !MatchesMapViewerToken(context.Request.Query["token"]))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (_mapViewers.Count >= MaximumMapViewers)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var id = Guid.NewGuid();
        var viewer = new BridgeClient { Socket = socket };
        _mapViewers[id] = viewer;
        await BroadcastViewerCountAsync();
        try
        {
            await SendAsync(viewer, "bridge.status", new
            {
                connected = !_clients.IsEmpty,
                mapEnabled = _clients.Values.Any(client => client.MapEnabled),
            }, context.RequestAborted);
            await SendAsync(viewer, "map.config", _mapConfig, context.RequestAborted);
            await SendAsync(viewer, "map.state", _mapState, context.RequestAborted);
            await SendAsync(viewer, "map.blips", _mapBlips, context.RequestAborted);
            await SendAsync(viewer, "map.units", _mapUnits, context.RequestAborted);
            await SendAsync(viewer, "map.alerts", _mapAlerts, context.RequestAborted);
            // The map only reads, except two commands the pause map also offers: set / clear the waypoint. They are
            // relayed to the game only when the game allows it (map.config waypoint) and at most ~3 per second.
            var buffer = new byte[MaximumViewerMessageBytes];
            var lastCommand = DateTimeOffset.MinValue;
            while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
            {
                var message = await ReceiveEnvelopeAsync(socket, buffer, context.RequestAborted);
                if (message is null)
                {
                    if (socket.State != WebSocketState.Open) break;
                    continue;
                }
                var now = DateTimeOffset.UtcNow;
                if (now - lastCommand < ViewerCommandInterval) continue;
                lastCommand = now;
                await HandleViewerMessageAsync(message);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            _mapViewers.TryRemove(id, out _);
            await BroadcastViewerCountAsync();
        }
    }

    private async Task HandleViewerMessageAsync(ClientEnvelope message)
    {
        if (!_mapConfig.Waypoint) return;
        object? command = message.Type switch
        {
            "map.waypoint" when ReadCoordinate(message.Data, "x", -5000, 6000) is { } x && ReadCoordinate(message.Data, "y", -5500, 9000) is { } y
                => new { action = "waypoint", x = Math.Round(x, 1), y = Math.Round(y, 1) },
            "map.clearWaypoint" => new { action = "clearWaypoint", x = 0, y = 0 },
            _ => null,
        };
        if (command is null) return;
        await BroadcastAsync(_clients.Values.Where(client => client.MapEnabled), "map.command", command);
    }

    private static double? ReadCoordinate(JsonElement data, string name, double min, double max)
        => data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number) &&
           number >= min && number <= max
            ? number : null;

    private Task BroadcastViewerCountAsync()
        => BroadcastAsync(_clients.Values, "map.viewers", new { count = _mapViewers.Count });

    private static bool IsAllowedNuiOrigin(string? origin)
        => !string.IsNullOrWhiteSpace(origin) && AllowedNuiOrigin.IsMatch(origin);

    private static bool IsTrustedZsxNuiOrigin(string? origin)
        => !string.IsNullOrWhiteSpace(origin) && TrustedZsxNuiOrigin.IsMatch(origin);

    private static bool IsAllowedMapOrigin(string? origin)
        => string.Equals(origin, $"http://127.0.0.1:{Port}", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(origin, $"http://localhost:{Port}", StringComparison.OrdinalIgnoreCase);

    private bool MatchesPairingToken(string? candidate)
        => MatchesToken(Settings.PairingToken, candidate);

    private bool MatchesMapViewerToken(string? candidate)
        => MatchesToken(Settings.MapViewerToken, candidate);

    private static bool MatchesToken(string expected, string? candidate)
    {
        if (candidate is null || candidate.Length != 64 || !candidate.All(Uri.IsHexDigit)) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(candidate.ToLowerInvariant()));
    }

    private async Task HandleClientMessageAsync(BridgeClient client, ClientEnvelope message, CancellationToken cancellationToken)
    {
        switch (message.Type)
        {
            case "media.control":
            {
                var action = ReadString(message.Data, "action", 16);
                if (action is not ("toggle" or "previous" or "next")) return;
                if (!await _media.ControlAsync(action))
                    await SendAsync(client, "bridge.error", new { message = "このメディアはその操作を受け付けません" }, cancellationToken);
                break;
            }
            case "media.config":
            {
                var wasMediaEnabled = client.MediaEnabled;
                client.MediaEnabled = ReadBoolean(message.Data, "mediaEnabled");
                client.SpectrumEnabled = client.MediaEnabled && ReadBoolean(message.Data, "spectrumEnabled");
                client.SpectrumBars = ReadInteger(message.Data, "spectrumBars", 5, 32, 11);
                client.SpectrumSensitivity = ReadDouble(message.Data, "spectrumSensitivity", 0.5, 3.0, 1.0);
                client.MapEnabled = ReadBoolean(message.Data, "mapEnabled");
                RefreshOptionalServices();
                // SMTC events are ignored while nobody displays media, so the
                // cached state may be stale when the widget is switched on.
                if (client.MediaEnabled && !wasMediaEnabled) _ = _media.RequestRefreshAsync();
                await BroadcastMapStatusAsync(true);
                break;
            }
            case "youtube.watch":
                // a video id / @handle / channel id or a YouTube URL of one; anything else is refused (only
                // www.youtube.com is ever contacted). "" stops.
                _youtube.Watch(ReadString(message.Data, "target", 200));
                break;
            case "map.state":
                if (!client.MapEnabled) return;
                _mapState = ParseMapState(message.Data);
                if (!_mapViewers.IsEmpty) await BroadcastAsync(_mapViewers.Values, "map.state", _mapState, droppable: true);
                break;
            case "map.blips":
                if (!client.MapEnabled) return;
                _mapBlips = new { blips = ParseBlips(message.Data) };
                if (!_mapViewers.IsEmpty) await BroadcastAsync(_mapViewers.Values, "map.blips", _mapBlips);
                break;
            case "map.config":
                if (!client.MapEnabled) return;
                _mapConfig = ParseMapConfig(message.Data);
                if (!_mapViewers.IsEmpty) await BroadcastAsync(_mapViewers.Values, "map.config", _mapConfig);
                break;
            case "map.units":
                if (!client.MapEnabled) return;
                _mapUnits = new { units = ParseUnits(message.Data) };
                if (!_mapViewers.IsEmpty) await BroadcastAsync(_mapViewers.Values, "map.units", _mapUnits);
                break;
            case "map.alerts":
                if (!client.MapEnabled) return;
                _mapAlerts = new { alerts = ParseAlerts(message.Data) };
                if (!_mapViewers.IsEmpty) await BroadcastAsync(_mapViewers.Values, "map.alerts", _mapAlerts);
                break;
        }
    }

    private static string ReadString(JsonElement data, string name, int maxLength)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return string.Empty;
        var text = (value.GetString() ?? string.Empty).Trim();
        return text.Length > maxLength ? text[..maxLength] : text;
    }

    private static bool ReadBoolean(JsonElement data, string name)
        => data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static int ReadInteger(JsonElement data, string name, int min, int max, int fallback)
        => data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? Math.Clamp(number, min, max) : fallback;

    private static double ReadDouble(JsonElement data, string name, double min, double max, double fallback)
        => data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number)
            ? Math.Clamp(number, min, max) : fallback;

    private static string ReadMapString(JsonElement data, string name)
        => ReadString(data, name, 80);

    private static CompanionMapState ParseMapState(JsonElement data) => new(
        Active: ReadBoolean(data, "active"),
        X: ReadDouble(data, "x", -10000, 10000, 0),
        Y: ReadDouble(data, "y", -10000, 10000, 0),
        Z: ReadDouble(data, "z", -2000, 4000, 0),
        Heading: ReadDouble(data, "heading", 0, 360, 0),
        SpeedKmh: ReadDouble(data, "speedKmh", 0, 1000, 0),
        Street: ReadMapString(data, "street"),
        Crossing: ReadMapString(data, "crossing"),
        Zone: ReadMapString(data, "zone"),
        InVehicle: ReadBoolean(data, "inVehicle"),
        HasWaypoint: ReadBoolean(data, "hasWaypoint"),
        WaypointX: ReadDouble(data, "waypointX", -10000, 10000, 0),
        WaypointY: ReadDouble(data, "waypointY", -10000, 10000, 0),
        Postal: PostalCode.IsMatch(ReadString(data, "postal", 8)) ? ReadString(data, "postal", 8) : string.Empty);

    private static readonly Regex PostalCode = new(@"^[A-Za-z0-9-]{1,8}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The postal tile base must be a plain http(s) URL ending in "/" (no credentials, query or fragment).</summary>
    private static CompanionMapConfig ParseMapConfig(JsonElement data)
    {
        var postal = ReadString(data, "postal", 300);
        if (postal.Length > 0 &&
            !(Uri.TryCreate(postal, UriKind.Absolute, out var uri) &&
              (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
              string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
              uri.AbsolutePath.EndsWith('/') && !postal.Contains('"') && !postal.Contains('\\')))
            postal = string.Empty;
        return new CompanionMapConfig(
            Waypoint: ReadBoolean(data, "waypoint"),
            Postal: postal,
            PostalMax: ReadInteger(data, "postalMax", 2, 7, 6),
            Services: ReadBoolean(data, "services"));
    }

    /// <summary>[[x, y, heading, kind, colour, name, group], ...] — numbers clamped, names cut, at most MaximumUnits</summary>
    private static object[][] ParseUnits(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("units", out var list) || list.ValueKind != JsonValueKind.Array)
            return Array.Empty<object[]>();
        var result = new List<object[]>();
        foreach (var item in list.EnumerateArray())
        {
            if (result.Count >= MaximumUnits) break;
            if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() < 4) continue;
            var values = item.EnumerateArray().ToArray();
            double Num(int i, double min, double max) =>
                i < values.Length && values[i].ValueKind == JsonValueKind.Number && values[i].TryGetDouble(out var n) && double.IsFinite(n)
                    ? Math.Clamp(n, min, max) : 0;
            string Str(int i, int max)
            {
                if (i >= values.Length || values[i].ValueKind != JsonValueKind.String) return string.Empty;
                var text = (values[i].GetString() ?? string.Empty).Trim();
                return text.Length > max ? text[..max] : text;
            }
            result.Add(new object[]
            {
                Math.Round(Num(0, -10000, 10000), 1), Math.Round(Num(1, -10000, 10000), 1), Math.Round(Num(2, 0, 360)),
                Math.Round(Num(3, 0, 9)), Math.Round(Num(4, 0, 255)), Str(5, 40), Str(6, 16),
            });
        }
        return result.ToArray();
    }

    private static CompanionMapAlert[] ParseAlerts(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("alerts", out var list) || list.ValueKind != JsonValueKind.Array)
            return Array.Empty<CompanionMapAlert>();
        var result = new List<CompanionMapAlert>();
        foreach (var item in list.EnumerateArray())
        {
            if (result.Count >= MaximumAlerts) break;
            if (item.ValueKind != JsonValueKind.Object) continue;
            result.Add(new CompanionMapAlert(
                Id: ReadInteger(item, "id", 0, int.MaxValue, 0),
                X: Math.Round(ReadDouble(item, "x", -10000, 10000, 0), 1),
                Y: Math.Round(ReadDouble(item, "y", -10000, 10000, 0), 1),
                Code: ReadString(item, "code", 16),
                Title: ReadString(item, "title", 80),
                Text: ReadString(item, "text", 120),
                Street: ReadString(item, "street", 80),
                Priority: ReadInteger(item, "priority", 0, 9, 2),
                At: (long)ReadDouble(item, "at", 0, 4102444800, 0)));
        }
        return result.ToArray();
    }

    /// <summary>[[sprite, colour, x, y, rotation], ...] — only numbers, clamped, at most MaximumBlips entries</summary>
    private static double[][] ParseBlips(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("blips", out var list) || list.ValueKind != JsonValueKind.Array)
            return Array.Empty<double[]>();
        var result = new List<double[]>();
        foreach (var item in list.EnumerateArray())
        {
            if (result.Count >= MaximumBlips) break;
            if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() < 4) continue;
            var values = new double[5];
            var ok = true;
            var index = 0;
            foreach (var v in item.EnumerateArray())
            {
                if (index >= 5) break;
                if (v.ValueKind != JsonValueKind.Number || !v.TryGetDouble(out var number) || !double.IsFinite(number)) { ok = false; break; }
                values[index++] = number;
            }
            if (!ok) continue;
            values[0] = Math.Clamp(Math.Round(values[0]), 0, 2000);
            values[1] = Math.Clamp(Math.Round(values[1]), 0, 255);
            values[2] = Math.Clamp(Math.Round(values[2], 1), -10000, 10000);
            values[3] = Math.Clamp(Math.Round(values[3], 1), -10000, 10000);
            values[4] = Math.Clamp(Math.Round(values[4]), 0, 360);
            result.Add(values);
        }
        return result.ToArray();
    }

    private void RefreshOptionalServices()
    {
        var requester = _clients.Values.FirstOrDefault(client => client.SpectrumEnabled);
        _spectrum.Configure(requester is not null, requester?.SpectrumBars ?? 11, requester?.SpectrumSensitivity ?? 1.0);
    }

    // Visualizer frames are disposable: a slow receiver skips frames instead
    // of queueing an ever-growing backlog of sends.
    private Task PublishSpectrumAsync(AudioSpectrumState spectrum)
        => BroadcastAsync(_clients.Values.Where(client => client.SpectrumEnabled), "audio.spectrum", spectrum, droppable: true);

    private Task PublishMediaAsync(MediaState media)
    {
        // Send embedded artwork only when it changes. A freshly paired client
        // receives CurrentState (which includes it), while existing clients
        // retain the last cover instead of receiving a large base64 image on
        // every one-second timeline refresh. Compare the media identity too:
        // consecutive tracks can legitimately share the same album art, and
        // the new track must still receive that image.
        var mediaKey = string.Join("\0", media.SourceApp, media.Title, media.Artist, media.Album, media.DurationMs);
        var outgoing = media;
        lock (_mediaBroadcastGate)
        {
            if (string.Equals(mediaKey, _lastBroadcastMediaKey, StringComparison.Ordinal) &&
                string.Equals(media.ArtworkDataUrl, _lastBroadcastArtwork, StringComparison.Ordinal))
                outgoing = media with { ArtworkDataUrl = string.Empty };
            else
            {
                _lastBroadcastMediaKey = mediaKey;
                _lastBroadcastArtwork = media.ArtworkDataUrl;
            }
        }
        return BroadcastAsync(_clients.Values, "media.state", outgoing);
    }

    private Task BroadcastMapStatusAsync(bool gameConnected)
        => BroadcastAsync(_mapViewers.Values, "bridge.status", new
        {
            connected = gameConnected && !_clients.IsEmpty,
            mapEnabled = _clients.Values.Any(client => client.MapEnabled),
        });

    private async Task BroadcastAsync(IEnumerable<BridgeClient> targets, string type, object data, bool droppable = false)
    {
        var recipients = targets.ToArray();
        if (recipients.Length == 0) return;
        // Serialize once and send to every receiver in parallel so one stalled
        // UI cannot delay the others.
        var payload = Serialize(type, data);
        CancellationToken cancellationToken;
        try { cancellationToken = _shutdown.Token; }
        catch (ObjectDisposedException) { return; }
        await Task.WhenAll(recipients.Select(async client =>
        {
            try { await SendRawAsync(client, payload, cancellationToken, droppable); }
            catch (Exception exception) when (exception is OperationCanceledException or WebSocketException or ObjectDisposedException)
            {
                // The receiver disconnected or timed out; its own loop cleans up.
            }
        }));
    }

    private static Task SendAsync(BridgeClient client, string type, object data, CancellationToken cancellationToken)
        => SendRawAsync(client, Serialize(type, data), cancellationToken, droppable: false);

    private static byte[] Serialize(string type, object data)
        => JsonSerializer.SerializeToUtf8Bytes(new { type, data }, JsonOptions.Default);

    private static async Task SendRawAsync(BridgeClient client, byte[] payload, CancellationToken cancellationToken, bool droppable)
    {
        if (client.Socket.State != WebSocketState.Open) return;
        if (droppable)
        {
            if (!await client.SendLock.WaitAsync(0, cancellationToken)) return;
        }
        else await client.SendLock.WaitAsync(cancellationToken);
        try
        {
            if (client.Socket.State != WebSocketState.Open) return;
            // A receiver that stops reading (frozen game, suspended CEF) is
            // aborted after the timeout instead of blocking every broadcast.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(SendTimeout);
            await client.Socket.SendAsync(payload, WebSocketMessageType.Text, true, timeout.Token);
        }
        finally
        {
            client.SendLock.Release();
        }
    }

    private static async Task<ClientEnvelope?> ReceiveEnvelopeAsync(WebSocket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        var received = 0;
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(received), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.MessageType != WebSocketMessageType.Text || result.Count == 0 || received + result.Count >= buffer.Length)
                return null;
            received += result.Count;
            if (result.EndOfMessage) break;
        }
        try { return JsonSerializer.Deserialize<ClientEnvelope>(buffer.AsSpan(0, received), JsonOptions.Default); }
        catch (JsonException) { return null; }
    }

    private static async Task CloseAsync(WebSocket socket, WebSocketCloseStatus status, string reason, CancellationToken cancellationToken)
    {
        try { await socket.CloseAsync(status, reason, cancellationToken); }
        catch (Exception) { /* Socket may already be closed. */ }
    }

    private string CreateLandingPage()
    {
        return $$"""
<!doctype html><html lang="ja"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><meta name="referrer" content="no-referrer"><title>MioCity Media Link</title>
<style>*{box-sizing:border-box}body{margin:0;background:#f2f7fa;color:#1f313e;font-family:system-ui,"Yu Gothic UI",sans-serif;display:grid;place-items:center;min-height:100vh}.card{width:min(600px,calc(100% - 40px));padding:36px;border:1px solid #dce8ee;background:#fff;border-radius:18px;box-shadow:0 18px 50px #17394d18}.brand{display:flex;align-items:center;gap:14px}.mark{display:grid;place-items:center;width:48px;height:48px;border-radius:12px;background:#0ea9cd;color:#fff;font-size:26px;font-weight:900}.eyebrow{margin:0;color:#0ea9cd;font-weight:800;letter-spacing:.12em;font-size:12px}.status{margin:22px 0;padding:14px;border-radius:10px;background:#e8f8f1;color:#147955;font-weight:700}h1{margin:3px 0 0;font-size:24px}p{line-height:1.75;color:#5c717f}.credit{margin-top:24px;color:#899aa5;font-size:13px}</style>
<main class="card"><div class="brand"><div class="mark">M</div><div><p class="eyebrow">MIOCITY EXCLUSIVE</p><h1>MioCity Media Link</h1></div></div><p class="status">● Bridge起動中 · 127.0.0.1:{{Port}}</p><p>FiveMでMioCityのUIを開くと、このPC内だけで自動的に接続します。キーのコピーやパスワード入力は不要です。</p><p>FiveM未接続時はメディア更新と波形取得を自動休止します。外部には公開されず、情報をMioCityサーバーへ送信することもありません。</p><p>セカンドモニターマップはBridge本体またはFiveM内の設定ボタンから開いてください。</p><p class="credit">Created by nesiddo</p></main>
</html>
""";
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0) return;
        _shutdown.Cancel();
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        foreach (var client in _clients.Values)
        {
            await CloseAsync(client.Socket, WebSocketCloseStatus.NormalClosure, "Bridge stopped", stopTimeout.Token);
            if (client.Socket.State is not (WebSocketState.Closed or WebSocketState.Aborted)) client.Socket.Abort();
        }
        _clients.Clear();
        foreach (var viewer in _mapViewers.Values)
        {
            await CloseAsync(viewer.Socket, WebSocketCloseStatus.NormalClosure, "Bridge stopped", stopTimeout.Token);
            if (viewer.Socket.State is not (WebSocketState.Closed or WebSocketState.Aborted)) viewer.Socket.Abort();
        }
        _mapViewers.Clear();
        _spectrum.Dispose();
        await _youtube.DisposeAsync();
        await _media.DisposeAsync();
        if (_application is not null)
        {
            try { await _application.StopAsync(stopTimeout.Token); }
            catch (OperationCanceledException) { /* The process can exit after the bounded graceful stop. */ }
        }
        _shutdown.Dispose();
    }
}
