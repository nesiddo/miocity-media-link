using System.Security.Cryptography;
using System.Text.Json;

namespace MioCity.LocalMediaBridge;

public sealed class BridgeSettings
{
    public string PairingToken { get; init; } = string.Empty;
    public string MapViewerToken { get; init; } = string.Empty;

    public static BridgeSettings LoadOrCreate()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MioCity", "LocalMediaBridge");
        var path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);

        try
        {
            if (File.Exists(path))
            {
                var existing = JsonSerializer.Deserialize<BridgeSettings>(File.ReadAllText(path));
                if (existing is { PairingToken.Length: 64 } && existing.PairingToken.All(Uri.IsHexDigit))
                {
                    var loaded = new BridgeSettings
                    {
                        PairingToken = existing.PairingToken.ToLowerInvariant(),
                        MapViewerToken = IsValidToken(existing.MapViewerToken) ? existing.MapViewerToken.ToLowerInvariant() : CreateToken(),
                    };
                    // Upgrade older settings files once so the map URL also has
                    // an independent random secret.
                    if (!IsValidToken(existing.MapViewerToken))
                        File.WriteAllText(path, JsonSerializer.Serialize(loaded, JsonOptions.Indented));
                    return loaded;
                }
            }
        }
        catch (Exception)
        {
            // An invalid local file is replaced with a new random pairing key.
        }

        var created = new BridgeSettings { PairingToken = CreateToken(), MapViewerToken = CreateToken() };
        File.WriteAllText(path, JsonSerializer.Serialize(created, JsonOptions.Indented));
        return created;
    }

    private static bool IsValidToken(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static string CreateToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}

public sealed record MediaState(
    bool Active,
    string SourceApp,
    string Title,
    string Artist,
    string Album,
    string ArtworkDataUrl,
    bool IsPlaying,
    long PositionMs,
    long DurationMs,
    bool CanPlayPause,
    bool CanPrevious,
    bool CanNext)
{
    public static readonly MediaState Empty = new(false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, 0, 0, false, false, false);
}

public sealed class BridgeClient
{
    public required System.Net.WebSockets.WebSocket Socket { get; init; }
    public SemaphoreSlim SendLock { get; } = new(1, 1);
    public bool MediaEnabled { get; set; }
    public bool SpectrumEnabled { get; set; }
    public int SpectrumBars { get; set; } = 11;
    public double SpectrumSensitivity { get; set; } = 1.0;
    public bool MapEnabled { get; set; }
}

public sealed record AudioSpectrumState(float[] Bars);

public sealed record CompanionMapState(
    bool Active,
    double X,
    double Y,
    double Z,
    double Heading,
    double SpeedKmh,
    string Street,
    string Crossing,
    string Zone,
    bool InVehicle,
    bool HasWaypoint,
    double WaypointX,
    double WaypointY,
    string Postal)
{
    public static readonly CompanionMapState Empty = new(false, 0, 0, 0, 0, 0, string.Empty, string.Empty, string.Empty, false, false, 0, 0, string.Empty);
}

/// <summary>What the game lets the companion map offer (sent by the game UI, app 1.4.0+).</summary>
public sealed record CompanionMapConfig(bool Waypoint, string Postal, int PostalMax, bool Services)
{
    public static readonly CompanionMapConfig Empty = new(false, string.Empty, 6, false);
}

/// <summary>A dispatch call shown on the companion map.</summary>
public sealed record CompanionMapAlert(int Id, double X, double Y, string Code, string Title, string Text, string Street, int Priority, long At);

public sealed class ClientEnvelope
{
    public string? Type { get; set; }
    public int Protocol { get; set; }
    public string? Token { get; set; }
    public bool RequestPairing { get; set; }
    public JsonElement Data { get; set; }
}

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public static readonly JsonSerializerOptions Indented = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}
