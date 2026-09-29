using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace MioCity.LocalMediaBridge;

/// <summary>
/// Reads only the Windows Global System Media Transport Controls session. It
/// never enumerates processes, reads memory, nor attaches to any game process.
/// </summary>
public sealed class MediaSessionService : IAsyncDisposable
{
    private readonly Func<bool> _shouldPoll;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly object _sessionGate = new();
    private int _refreshRequested;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _timerCancellation;
    private MediaState _lastState = MediaState.Empty;
    private string _artworkKey = string.Empty;
    private string _artworkDataUrl = string.Empty;
    private int _artworkReadAttempts;
    private DateTimeOffset _nextArtworkReadAt = DateTimeOffset.MinValue;

    public MediaSessionService(Func<bool>? shouldPoll = null)
    {
        _shouldPoll = shouldPoll ?? (() => true);
    }

    public event Func<MediaState, Task>? StateChanged;
    public string Status { get; private set; } = "Windows media session を待機しています";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            _manager.SessionsChanged += OnSessionsChanged;
            SelectCurrentSession();
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            _timerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = RefreshLoopAsync(_timerCancellation.Token);
            Status = "Windows media session に接続済み";
        }
        catch (Exception exception)
        {
            Status = "Windows media session を利用できません: " + exception.Message;
            await PublishAsync(MediaState.Empty);
        }
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_timer is not null && await _timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_shouldPoll()) await RefreshAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal application shutdown.
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        => SelectCurrentSession();

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        => SelectCurrentSession();

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        => RefreshIfWatched();

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        => RefreshIfWatched();

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        => RefreshIfWatched();

    // While FiveM is closed (or the widget is off) SMTC events do not read
    // metadata or artwork. RequestRefreshAsync catches up when a player
    // enables the widget again.
    private void RefreshIfWatched()
    {
        if (_shouldPoll()) _ = RefreshAsync();
    }

    public Task RequestRefreshAsync() => RefreshAsync();

    private void SelectCurrentSession()
    {
        try
        {
            var next = _manager?.GetCurrentSession();
            // Session events arrive on arbitrary threads; swap subscriptions
            // atomically so one session never receives duplicate handlers.
            lock (_sessionGate)
            {
                if (!ReferenceEquals(next, _session))
                {
                    if (_session is not null)
                    {
                        _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                        _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                        _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
                    }

                    _session = next;
                    ResetArtwork();
                    if (_session is not null)
                    {
                        _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                        _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                        _session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Status = "メディアセッションの切替に失敗しました: " + exception.Message;
        }
        RefreshIfWatched();
    }

    public async Task<bool> ControlAsync(string? action)
    {
        var session = _session;
        if (session is null) return false;

        bool ok = action switch
        {
            "toggle" => await session.TryTogglePlayPauseAsync(),
            "previous" => await session.TrySkipPreviousAsync(),
            "next" => await session.TrySkipNextAsync(),
            _ => false,
        };
        if (ok) await RefreshAsync();
        return ok;
    }

    private async Task RefreshAsync()
    {
        // Coalesce concurrent requests without dropping them: an event that
        // arrives while a refresh is running (typically a track change) makes
        // the running refresh loop once more instead of being ignored.
        Interlocked.Exchange(ref _refreshRequested, 1);
        while (true)
        {
            if (!await _refreshLock.WaitAsync(0)) return;
            try
            {
                while (Interlocked.Exchange(ref _refreshRequested, 0) == 1)
                    await RefreshCoreAsync();
            }
            finally
            {
                _refreshLock.Release();
            }
            if (Volatile.Read(ref _refreshRequested) == 0) return;
        }
    }

    private async Task RefreshCoreAsync()
    {
        try
        {
            var session = _session;
            if (session is null)
            {
                await PublishAsync(MediaState.Empty);
                return;
            }

            var properties = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            var controls = playback.Controls;
            var source = ResolveSourceName(session.SourceAppUserModelId);
            var artworkKey = string.Join("\0", session.SourceAppUserModelId, properties?.Title, properties?.Artist, properties?.AlbumTitle);
            if (!string.Equals(artworkKey, _artworkKey, StringComparison.Ordinal))
            {
                _artworkKey = artworkKey;
                _artworkDataUrl = string.Empty;
                _artworkReadAttempts = 0;
                _nextArtworkReadAt = DateTimeOffset.MinValue;
            }

            // SMTC providers commonly publish title/artist first and attach
            // the thumbnail a little later. Chromium-based browsers in
            // particular can need several refreshes. An empty first result is
            // therefore retried instead of being cached for the whole track.
            if (string.IsNullOrEmpty(_artworkDataUrl) && DateTimeOffset.UtcNow >= _nextArtworkReadAt)
            {
                _artworkReadAttempts++;
                _artworkDataUrl = await ReadArtworkDataUrlAsync(properties?.Thumbnail);
                if (string.IsNullOrEmpty(_artworkDataUrl))
                {
                    var retryDelay = _artworkReadAttempts <= 10
                        ? TimeSpan.FromSeconds(1)
                        : TimeSpan.FromSeconds(5);
                    _nextArtworkReadAt = DateTimeOffset.UtcNow.Add(retryDelay);
                }
            }
            var next = new MediaState(
                Active: true,
                SourceApp: source,
                Title: properties?.Title ?? string.Empty,
                Artist: properties?.Artist ?? string.Empty,
                Album: properties?.AlbumTitle ?? string.Empty,
                ArtworkDataUrl: _artworkDataUrl,
                IsPlaying: playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                PositionMs: Math.Max(0, (long)timeline.Position.TotalMilliseconds),
                DurationMs: Math.Max(0, (long)timeline.EndTime.TotalMilliseconds),
                CanPlayPause: controls?.IsPlayPauseToggleEnabled == true || controls?.IsPlayEnabled == true || controls?.IsPauseEnabled == true,
                CanPrevious: controls?.IsPreviousEnabled == true,
                CanNext: controls?.IsNextEnabled == true);
            await PublishAsync(next);
        }
        catch (Exception exception)
        {
            Status = "メディア情報の取得に失敗しました: " + exception.Message;
            await PublishAsync(MediaState.Empty);
        }
    }

    private static async Task<string> ReadArtworkDataUrlAsync(IRandomAccessStreamReference? thumbnail)
    {
        // Browser SMTC thumbnails are sometimes full-size source images. They
        // are read once, then reduced to a small JPEG: the widget shows the
        // cover at well under 200 px, and a multi-megabyte base64 string
        // would otherwise travel over the WebSocket and sit in CEF memory.
        const ulong maximumSourceBytes = 16 * 1024 * 1024;
        const int passthroughBytes = 200 * 1024;
        const int fallbackBytes = 1024 * 1024;
        const uint maximumEdge = 320;
        if (thumbnail is null) return string.Empty;
        try
        {
            byte[] bytes;
            string? contentType;
            using (var stream = await thumbnail.OpenReadAsync())
            {
                if (stream.Size == 0 || stream.Size > maximumSourceBytes) return string.Empty;
                using var reader = new DataReader(stream);
                await reader.LoadAsync((uint)stream.Size);
                bytes = new byte[(int)stream.Size];
                reader.ReadBytes(bytes);
                contentType = stream.ContentType?.ToLowerInvariant();
            }
            if (contentType is not ("image/jpeg" or "image/png" or "image/webp")) contentType = DetectArtworkContentType(bytes);

            var resized = await TryResizeArtworkAsync(bytes, maximumEdge, contentType is not null && bytes.Length <= passthroughBytes);
            if (resized is not null)
                return resized.Length == 0
                    ? $"data:{contentType};base64,{Convert.ToBase64String(bytes)}"
                    : $"data:image/jpeg;base64,{Convert.ToBase64String(resized)}";
            // No usable codec (for example WebP without the Windows
            // extension): keep small originals, drop oversized ones.
            return contentType is null || bytes.Length > fallbackBytes
                ? string.Empty
                : $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Returns a downscaled JPEG, an empty array when the original is already
    /// small enough to pass through unchanged, or null when decoding fails.
    /// </summary>
    private static async Task<byte[]?> TryResizeArtworkAsync(byte[] source, uint maximumEdge, bool allowPassthrough)
    {
        try
        {
            using var input = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(input))
            {
                writer.WriteBytes(source);
                await writer.StoreAsync();
                writer.DetachStream();
            }
            input.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(input);
            var width = decoder.PixelWidth;
            var height = decoder.PixelHeight;
            if (width == 0 || height == 0) return null;
            if (allowPassthrough && Math.Max(width, height) <= maximumEdge * 2) return Array.Empty<byte>();

            var scale = Math.Min(1.0, (double)maximumEdge / Math.Max(width, height));
            var transform = new BitmapTransform
            {
                ScaledWidth = Math.Max(1u, (uint)Math.Round(width * scale)),
                ScaledHeight = Math.Max(1u, (uint)Math.Round(height * scale)),
                InterpolationMode = BitmapInterpolationMode.Fant,
            };
            using var pixels = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, transform,
                ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.ColorManageToSRgb);

            using var output = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output, new BitmapPropertySet
            {
                { "ImageQuality", new BitmapTypedValue(0.86f, Windows.Foundation.PropertyType.Single) },
            });
            encoder.SetSoftwareBitmap(pixels);
            await encoder.FlushAsync();

            var size = (uint)output.Size;
            if (size == 0) return null;
            using var reader = new DataReader(output.GetInputStreamAt(0));
            await reader.LoadAsync(size);
            var result = new byte[size];
            reader.ReadBytes(result);
            return result;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void ResetArtwork()
    {
        _artworkKey = string.Empty;
        _artworkDataUrl = string.Empty;
        _artworkReadAttempts = 0;
        _nextArtworkReadAt = DateTimeOffset.MinValue;
    }

    private static string? DetectArtworkContentType(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4e && bytes[3] == 0x47) return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) return "image/jpeg";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return "image/webp";
        return null;
    }

    private static string ResolveSourceName(string? appUserModelId)
    {
        if (string.IsNullOrWhiteSpace(appUserModelId)) return "MEDIA APP";
        try
        {
            var app = AppInfo.GetFromAppUserModelId(appUserModelId);
            if (!string.IsNullOrWhiteSpace(app?.DisplayInfo.DisplayName)) return app.DisplayInfo.DisplayName;
        }
        catch (Exception)
        {
            // Some desktop sessions have no resolvable AppInfo. Use a compact
            // neutral identifier instead of a provider-specific fallback.
        }
        var name = appUserModelId.Split('!')[0];
        return name.Length > 32 ? name[..32] : name;
    }

    private async Task PublishAsync(MediaState next)
    {
        _lastState = next;
        var listeners = StateChanged;
        if (listeners is null) return;
        foreach (var listener in listeners.GetInvocationList().Cast<Func<MediaState, Task>>())
        {
            try { await listener(next); }
            catch (Exception) { /* A disconnected local UI must not stop SMTC. */ }
        }
    }

    public MediaState CurrentState => _lastState;

    public async ValueTask DisposeAsync()
    {
        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _manager.SessionsChanged -= OnSessionsChanged;
        }
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        }
        if (_timerCancellation is not null) await _timerCancellation.CancelAsync();
        _timer?.Dispose();
        _timerCancellation?.Dispose();
        // _refreshLock is not disposed: a late SMTC event may still call
        // RefreshAsync, and the semaphore owns no native handle.
    }
}
