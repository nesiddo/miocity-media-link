using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MioCity.LocalMediaBridge;

/// <summary>One part of a chat message: plain text, or a channel's custom emoji (an image URL on YouTube's image host).</summary>
public sealed record YouTubeChatPart(string? Text, string? Image, string? Alt);

/// <summary>
/// A chat line. Kind: "text" | "paid" (Super Chat / Super Sticker) | "member" (new member / milestone).
/// Role: "owner" | "moderator" | "member" | "verified" | "".
/// </summary>
public sealed record YouTubeChatMessage(string Id, string Author, string Kind, string Role, string Amount, string Color, YouTubeChatPart[] Parts, long At);

/// <summary>State: "idle" | "resolving" | "live" | "offline" | "error".</summary>
public sealed record YouTubeChatStatus(string State, string Target, string Title, string Channel, string VideoId, string Message)
{
    public static readonly YouTubeChatStatus Idle = new("idle", "", "", "", "", "");
}

/// <summary>
/// Reads the public live chat of a YouTube stream the way the YouTube web page does (no API key, no sign-in): the popout
/// chat page gives a continuation token, then youtubei/v1/live_chat/get_live_chat is polled with it. This is not an
/// official API; when YouTube changes the page the status becomes "error" and nothing else breaks.
/// Only www.youtube.com is contacted, and only for a target that parsed as a video id, a @handle or a UC channel id.
/// </summary>
public sealed class YouTubeChatService : IAsyncDisposable
{
    private const string Origin = "https://www.youtube.com";
    private const int MaximumPageBytes = 6 * 1024 * 1024;
    private const int MaximumTextLength = 300;
    private const int MaximumParts = 40;
    private const int HistoryOnStart = 5;
    private static readonly TimeSpan OfflineRetry = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ErrorRetry = TimeSpan.FromSeconds(30);
    private static readonly Regex VideoId = new(@"^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Handle = new(@"^@[A-Za-z0-9._-]{3,30}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ChannelId = new(@"^UC[A-Za-z0-9_-]{22}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Canonical = new(@"<link rel=""canonical"" href=""https://www\.youtube\.com/watch\?v=([A-Za-z0-9_-]{11})""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    private static readonly Regex ApiKey = new(@"""INNERTUBE_API_KEY"":""([A-Za-z0-9_-]{20,80})""", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    private static readonly Regex ClientVersion = new(@"""INNERTUBE_CONTEXT_CLIENT_VERSION"":""([0-9.]{5,40})""", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    private static readonly Regex VisitorData = new(@"""VISITOR_DATA"":""([A-Za-z0-9%_=-]{10,200})""", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    private static readonly Regex InitialData = new(@"(?:window\[""ytInitialData""\]|var ytInitialData)\s*=\s*(\{.+?\})\s*;\s*</script>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromSeconds(3));

    private readonly HttpClient _http;
    private readonly object _gate = new();
    private CancellationTokenSource? _run;
    private string _target = string.Empty;

    public YouTubeChatService(HttpMessageHandler? handler = null)
    {
        _http = new HttpClient(handler ?? new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            UseCookies = false,
        })
        { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ja,en;q=0.8");
        // skips the consent page shown in some regions
        _http.DefaultRequestHeaders.Add("Cookie", "CONSENT=YES+1");
    }

    public event Func<YouTubeChatStatus, Task>? StatusChanged;
    public event Func<IReadOnlyList<YouTubeChatMessage>, Task>? MessagesReceived;
    public YouTubeChatStatus Status { get; private set; } = YouTubeChatStatus.Idle;

    /// <summary>
    /// "v:VIDEOID" | "h:@handle" | "c:UC..." for a video id / @handle / channel id or a youtube.com / youtu.be URL of one;
    /// null for anything else.
    /// </summary>
    public static string? ParseTarget(string? input)
    {
        var text = (input ?? string.Empty).Trim();
        if (text.Length is 0 or > 200) return null;
        if (VideoId.IsMatch(text)) return "v:" + text;
        if (Handle.IsMatch(text)) return "h:" + text;
        if (ChannelId.IsMatch(text)) return "c:" + text;
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return null;
        var host = uri.Host.ToLowerInvariant();
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (host == "youtu.be") return segments.Length > 0 && VideoId.IsMatch(segments[0]) ? "v:" + segments[0] : null;
        if (host is not ("youtube.com" or "www.youtube.com" or "m.youtube.com" or "gaming.youtube.com")) return null;
        if (segments.Length == 0) return null;
        if (segments[0] == "watch")
        {
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
                if (pair.StartsWith("v=", StringComparison.Ordinal) && VideoId.IsMatch(pair[2..])) return "v:" + pair[2..];
            return null;
        }
        if (segments[0] is "live" or "shorts" or "embed" && segments.Length > 1 && VideoId.IsMatch(segments[1])) return "v:" + segments[1];
        if (segments[0] == "live_chat")
        {
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
                if (pair.StartsWith("v=", StringComparison.Ordinal) && VideoId.IsMatch(pair[2..])) return "v:" + pair[2..];
            return null;
        }
        var handle = Uri.UnescapeDataString(segments[0]);
        if (Handle.IsMatch(handle)) return "h:" + handle;
        if (segments[0] == "channel" && segments.Length > 1 && ChannelId.IsMatch(segments[1])) return "c:" + segments[1];
        return null;
    }

    /// <summary>Follow a stream or channel ("" = stop). The same target again changes nothing.</summary>
    public void Watch(string? input)
    {
        var target = ParseTarget(input) ?? string.Empty;
        CancellationTokenSource? previous;
        CancellationTokenSource? next = null;
        lock (_gate)
        {
            if (target == _target && (_run is not null || target.Length == 0)) return;
            previous = _run;
            _target = target;
            _run = null;
            if (target.Length > 0)
            {
                next = new CancellationTokenSource();
                _run = next;
            }
        }
        previous?.Cancel();
        if (next is null)
        {
            var invalid = !string.IsNullOrWhiteSpace(input);
            _ = SetStatusAsync(invalid
                ? new YouTubeChatStatus("error", "", "", "", "", "YouTube の URL・@チャンネル名・動画IDとして読めません")
                : YouTubeChatStatus.Idle);
            return;
        }
        var token = next.Token;
        _ = Task.Run(() => RunAsync(target, token), token);
    }

    private async Task SetStatusAsync(YouTubeChatStatus status)
    {
        Status = status;
        var handler = StatusChanged;
        if (handler is null) return;
        try { await handler(status); } catch (Exception) { /* a closed receiver */ }
    }

    private async Task RunAsync(string target, CancellationToken cancellationToken)
    {
        var kind = target[0];
        var value = target[2..];
        while (!cancellationToken.IsCancellationRequested)
        {
            TimeSpan wait;
            try
            {
                await SetStatusAsync(new YouTubeChatStatus("resolving", target, "", "", "", "配信を探しています"));
                var videoId = kind == 'v' ? value : await ResolveLiveVideoAsync(kind == 'h' ? value : "channel/" + value, cancellationToken);
                if (videoId is null)
                {
                    await SetStatusAsync(new YouTubeChatStatus("offline", target, "", kind == 'h' ? value : "", "", "いまは配信していません。1分ごとに確かめます"));
                    wait = OfflineRetry;
                }
                else
                {
                    wait = await FollowChatAsync(target, videoId, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or RegexMatchTimeoutException or InvalidDataException or TaskCanceledException)
            {
                await SetStatusAsync(new YouTubeChatStatus("error", target, "", "", "", "YouTube から読み込めませんでした。30秒後にもう一度試します"));
                wait = ErrorRetry;
            }
            try { await Task.Delay(wait, cancellationToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>the video id of the channel's current live stream, or null when it is not live</summary>
    private async Task<string?> ResolveLiveVideoAsync(string channelPath, CancellationToken cancellationToken)
    {
        var page = await GetPageAsync($"{Origin}/{channelPath}/live", cancellationToken);
        var match = Canonical.Match(page);
        if (!match.Success) return null;
        // the /live page of an offline channel shows the channel or an upcoming / past stream
        return page.Contains("\"isLiveNow\":true", StringComparison.Ordinal) || page.Contains("\"isLive\":true", StringComparison.Ordinal)
            ? match.Groups[1].Value
            : null;
    }

    /// <summary>polls one stream's chat until it ends; returns how long to wait before looking again</summary>
    private async Task<TimeSpan> FollowChatAsync(string target, string videoId, CancellationToken cancellationToken)
    {
        var page = await GetPageAsync($"{Origin}/live_chat?is_popout=1&v={videoId}", cancellationToken);
        var key = ApiKey.Match(page);
        var version = ClientVersion.Match(page);
        var visitor = VisitorData.Match(page);
        var initial = InitialData.Match(page);
        if (!version.Success || !initial.Success) throw new InvalidDataException("chat page");
        string? continuation;
        using (var document = JsonDocument.Parse(initial.Groups[1].Value))
        {
            if (!document.RootElement.TryGetProperty("contents", out var contents) || !contents.TryGetProperty("liveChatRenderer", out var renderer))
            {
                await SetStatusAsync(new YouTubeChatStatus("offline", target, "", "", videoId, "この配信にはチャットがありません（終了したか、チャットがオフです）"));
                return OfflineRetry;
            }
            continuation = AllChatContinuation(renderer) ?? FirstContinuation(renderer);
        }
        if (continuation is null) throw new InvalidDataException("continuation");
        var (title, channel) = await ReadTitleAsync(videoId, cancellationToken);
        await SetStatusAsync(new YouTubeChatStatus("live", target, title, channel, videoId, ""));

        var endpoint = $"{Origin}/youtubei/v1/live_chat/get_live_chat?prettyPrint=false" + (key.Success ? "&key=" + key.Groups[1].Value : "");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var order = new Queue<string>();
        var first = true;
        var failures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var body = JsonSerializer.Serialize(new
            {
                context = new { client = new { clientName = "WEB", clientVersion = version.Groups[1].Value, hl = "ja", visitorData = visitor.Success ? Uri.UnescapeDataString(visitor.Groups[1].Value) : null } },
                continuation,
            });
            string json;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) break;
                response.EnsureSuccessStatusCode();
                json = await ReadLimitedAsync(response, cancellationToken);
                failures = 0;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                if (++failures >= 4) throw;
                await Task.Delay(TimeSpan.FromSeconds(3 * failures), cancellationToken);
                continue;
            }

            var (messages, next, timeoutMs) = ParseChatResponse(json);
            var fresh = new List<YouTubeChatMessage>(messages.Count);
            foreach (var message in messages)
            {
                if (!seen.Add(message.Id)) continue;
                order.Enqueue(message.Id);
                if (order.Count > 2000) seen.Remove(order.Dequeue());
                fresh.Add(message);
            }
            // the first answer carries the recent history: show only its last few lines, like joining a Twitch chat
            if (first && fresh.Count > HistoryOnStart) fresh = fresh.GetRange(fresh.Count - HistoryOnStart, HistoryOnStart);
            first = false;
            if (fresh.Count > 0 && MessagesReceived is { } handler)
            {
                try { await handler(fresh); } catch (Exception) { /* a closed receiver */ }
            }
            if (next is null)
            {
                // no continuation: the stream ended
                await SetStatusAsync(new YouTubeChatStatus("offline", target, title, channel, videoId, "配信が終わりました"));
                return OfflineRetry;
            }
            continuation = next;
            // YouTube's page is pushed new lines; without that push, ask about every 3 s (its own hint is up to 10 s)
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs, 1500, 3000)), cancellationToken);
        }
        await SetStatusAsync(new YouTubeChatStatus("offline", target, title, channel, videoId, "チャットを読めなくなりました（配信が終わった可能性があります）"));
        return OfflineRetry;
    }

    private static string? FirstContinuation(JsonElement renderer)
    {
        if (!renderer.TryGetProperty("continuations", out var list) || list.ValueKind != JsonValueKind.Array) return null;
        foreach (var entry in list.EnumerateArray())
            foreach (var kind in entry.EnumerateObject())
                if (kind.Value.ValueKind == JsonValueKind.Object && kind.Value.TryGetProperty("continuation", out var c) && c.ValueKind == JsonValueKind.String)
                    return c.GetString();
        return null;
    }

    /// <summary>"チャット" (every line) instead of the default "トップチャット" (YouTube's filtered selection)</summary>
    private static string? AllChatContinuation(JsonElement renderer)
    {
        try
        {
            var items = renderer.GetProperty("header").GetProperty("liveChatHeaderRenderer").GetProperty("viewSelector")
                .GetProperty("sortFilterSubMenuRenderer").GetProperty("subMenuItems");
            if (items.GetArrayLength() < 2) return null;
            return items[items.GetArrayLength() - 1].GetProperty("continuation").GetProperty("reloadContinuationData").GetProperty("continuation").GetString();
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    private async Task<(string Title, string Channel)> ReadTitleAsync(string videoId, CancellationToken cancellationToken)
    {
        try
        {
            var json = await GetPageAsync($"{Origin}/oembed?format=json&url=https%3A%2F%2Fwww.youtube.com%2Fwatch%3Fv%3D{videoId}", cancellationToken);
            using var document = JsonDocument.Parse(json);
            return (Cut(Str(document.RootElement, "title"), 120), Cut(Str(document.RootElement, "author_name"), 60));
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return ("", "");
        }
    }

    /// <summary>chat lines, the next continuation (null = ended) and YouTube's suggested wait</summary>
    public static (List<YouTubeChatMessage> Messages, string? Next, int TimeoutMs) ParseChatResponse(string json)
    {
        var messages = new List<YouTubeChatMessage>();
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("continuationContents", out var cc) || !cc.TryGetProperty("liveChatContinuation", out var lcc))
            return (messages, null, 0);
        if (lcc.TryGetProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            foreach (var action in actions.EnumerateArray())
            {
                if (!action.TryGetProperty("addChatItemAction", out var add) || !add.TryGetProperty("item", out var item)) continue;
                var message = ReadItem(item);
                if (message is not null) messages.Add(message);
            }
        }
        string? next = null;
        var timeout = 5000;
        if (lcc.TryGetProperty("continuations", out var conts) && conts.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in conts.EnumerateArray())
                foreach (var kind in entry.EnumerateObject())
                {
                    if (kind.Value.ValueKind != JsonValueKind.Object) continue;
                    if (kind.Value.TryGetProperty("continuation", out var c) && c.ValueKind == JsonValueKind.String) next ??= c.GetString();
                    if (kind.Value.TryGetProperty("timeoutMs", out var t) && t.TryGetInt32(out var ms)) timeout = ms;
                }
        }
        return (messages, next, timeout);
    }

    private static YouTubeChatMessage? ReadItem(JsonElement item)
    {
        string kind;
        JsonElement r;
        if (item.TryGetProperty("liveChatTextMessageRenderer", out r)) kind = "text";
        else if (item.TryGetProperty("liveChatPaidMessageRenderer", out r)) kind = "paid";
        else if (item.TryGetProperty("liveChatPaidStickerRenderer", out r)) kind = "paid";
        else if (item.TryGetProperty("liveChatMembershipItemRenderer", out r)) kind = "member";
        else return null;
        var id = Str(r, "id");
        if (id.Length is 0 or > 200) return null;
        var author = Cut(r.TryGetProperty("authorName", out var an) ? Str(an, "simpleText") : "", 60);
        var parts = new List<YouTubeChatPart>();
        var length = 0;
        if (kind == "member" && r.TryGetProperty("headerSubtext", out var sub)) AddRuns(sub, parts, ref length);
        if (r.TryGetProperty("message", out var message)) AddRuns(message, parts, ref length);
        if (parts.Count == 0 && kind == "paid") parts.Add(new YouTubeChatPart("スーパーステッカー", null, null));
        if (parts.Count == 0 && kind == "text") return null;
        var amount = kind == "paid" && r.TryGetProperty("purchaseAmountText", out var pa) ? Cut(Str(pa, "simpleText"), 24) : "";
        var color = "";
        if (kind == "paid" && (r.TryGetProperty("bodyBackgroundColor", out var bg) || r.TryGetProperty("backgroundColor", out bg)) && bg.TryGetInt64(out var argb))
            color = "#" + ((uint)argb & 0xFFFFFF).ToString("x6", System.Globalization.CultureInfo.InvariantCulture);
        var role = "";
        if (r.TryGetProperty("authorBadges", out var badges) && badges.ValueKind == JsonValueKind.Array)
        {
            foreach (var badge in badges.EnumerateArray())
            {
                if (!badge.TryGetProperty("liveChatAuthorBadgeRenderer", out var b)) continue;
                var icon = b.TryGetProperty("icon", out var ic) ? Str(ic, "iconType") : "";
                var found = icon switch { "OWNER" => "owner", "MODERATOR" => "moderator", "VERIFIED" => "verified", _ => b.TryGetProperty("customThumbnail", out _) ? "member" : "" };
                if (Rank(found) > Rank(role)) role = found;
            }
        }
        long at = 0;
        if (long.TryParse(Str(r, "timestampUsec"), out var usec)) at = usec / 1000;
        return new YouTubeChatMessage(id, author, kind, role, amount, color, parts.ToArray(), at);
    }

    private static int Rank(string role) => role switch { "owner" => 4, "moderator" => 3, "member" => 2, "verified" => 1, _ => 0 };

    /// <summary>text runs, standard emoji as their character, a channel's custom emoji as an image on YouTube's host</summary>
    private static void AddRuns(JsonElement holder, List<YouTubeChatPart> parts, ref int length)
    {
        if (holder.TryGetProperty("simpleText", out var simple) && simple.ValueKind == JsonValueKind.String)
        {
            AddText(simple.GetString() ?? "", parts, ref length);
            return;
        }
        if (!holder.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Array) return;
        foreach (var run in runs.EnumerateArray())
        {
            if (parts.Count >= MaximumParts || length >= MaximumTextLength) return;
            if (run.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String) AddText(text.GetString() ?? "", parts, ref length);
            else if (run.TryGetProperty("emoji", out var emoji))
            {
                var custom = emoji.TryGetProperty("isCustomEmoji", out var ce) && ce.ValueKind == JsonValueKind.True;
                var alt = "";
                if (emoji.TryGetProperty("shortcuts", out var sc) && sc.ValueKind == JsonValueKind.Array && sc.GetArrayLength() > 0) alt = Cut(sc[0].GetString() ?? "", 40);
                if (!custom)
                {
                    AddText(Cut(Str(emoji, "emojiId"), 16), parts, ref length);
                    continue;
                }
                var url = EmojiUrl(emoji);
                if (url is null) AddText(alt, parts, ref length);
                else
                {
                    parts.Add(new YouTubeChatPart(null, url, alt));
                    length += 2;
                }
            }
        }
    }

    private static void AddText(string text, List<YouTubeChatPart> parts, ref int length)
    {
        if (text.Length == 0 || length >= MaximumTextLength) return;
        text = text.Replace('\n', ' ').Replace('\r', ' ');
        if (length + text.Length > MaximumTextLength) text = text[..(MaximumTextLength - length)];
        length += text.Length;
        if (parts.Count > 0 && parts[^1].Text is { } prev) parts[^1] = new YouTubeChatPart(prev + text, null, null);
        else parts.Add(new YouTubeChatPart(text, null, null));
    }

    /// <summary>the emoji's smallest image, only from YouTube's own image hosts</summary>
    private static string? EmojiUrl(JsonElement emoji)
    {
        if (!emoji.TryGetProperty("image", out var image) || !image.TryGetProperty("thumbnails", out var thumbs) || thumbs.ValueKind != JsonValueKind.Array) return null;
        foreach (var thumb in thumbs.EnumerateArray())
        {
            var url = Str(thumb, "url");
            if (url.StartsWith("//", StringComparison.Ordinal)) url = "https:" + url;
            if (url.Length > 400 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) continue;
            var host = uri.Host.ToLowerInvariant();
            if (host.EndsWith(".ggpht.com", StringComparison.Ordinal) || host.EndsWith(".googleusercontent.com", StringComparison.Ordinal) ||
                host is "www.youtube.com" or "www.gstatic.com" or "fonts.gstatic.com")
                return uri.AbsoluteUri;
        }
        return null;
    }

    private async Task<string> GetPageAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadLimitedAsync(response, cancellationToken);
    }

    private static async Task<string> ReadLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaximumPageBytes) throw new InvalidDataException("too large");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + read > MaximumPageBytes) throw new InvalidDataException("too large");
            memory.Write(buffer, 0, read);
        }
        return Encoding.UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length);
    }

    private static string Str(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static string Cut(string text, int max) => text.Length > max ? text[..max] : text;

    public ValueTask DisposeAsync()
    {
        CancellationTokenSource? run;
        lock (_gate)
        {
            run = _run;
            _run = null;
            _target = string.Empty;
        }
        run?.Cancel();
        _http.Dispose();
        return ValueTask.CompletedTask;
    }
}
