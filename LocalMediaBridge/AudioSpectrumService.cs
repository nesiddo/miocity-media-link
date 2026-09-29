using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Dsp;
using NAudio.Wave;

namespace MioCity.LocalMediaBridge;

/// <summary>
/// Converts Windows output audio into short-lived frequency-band levels. Raw
/// samples live only in a small in-memory window and are never written to
/// disk or sent anywhere; only the normalized bar heights leave this class.
/// </summary>
public sealed class AudioSpectrumService : IDisposable
{
    private const int FftExponent = 11;
    private const int FftSize = 1 << FftExponent; // 2048 samples ≈ 43 ms at 48 kHz
    private const long PublishIntervalMs = 50;     // at most 20 frames per second
    private const double MinimumFrequency = 45;
    private const double MaximumFrequency = 16000;
    private const double FloorDecibels = -62;

    private static readonly float[] Window = CreateWindow();

    private readonly object _gate = new();
    private readonly float[] _history = new float[FftSize];
    private readonly Complex[] _fft = new Complex[FftSize];
    private WasapiLoopbackCapture? _capture;
    private MMDeviceEnumerator? _deviceEnumerator;
    private DeviceChangeListener? _deviceListener;
    private bool _wanted;
    private int _historyWrite;
    private int _historyFilled;
    private int _barCount = 11;
    private double _sensitivity = 1.0;
    private long _lastPublishTicks;
    private float[] _smoothed = new float[11];
    private int _publishing;
    private int _restartQueued;
    private bool _disposed;

    public event Func<AudioSpectrumState, Task>? SpectrumChanged;
    public string Status { get; private set; } = "波形は停止中";

    public void Configure(bool enabled, int bars, double sensitivity)
    {
        bars = Math.Clamp(bars, 5, 32);
        sensitivity = Math.Clamp(sensitivity, 0.5, 3.0);
        WasapiLoopbackCapture? stopped = null;
        lock (_gate)
        {
            if (_disposed) return;
            _wanted = enabled;
            _barCount = bars;
            _sensitivity = sensitivity;
            if (_smoothed.Length != bars) _smoothed = new float[bars];
            if (enabled && _capture is null) StartCaptureLocked();
            else if (!enabled && _capture is not null) stopped = DetachCaptureLocked("FiveM未接続または波形OFFのため休止中");
        }
        // Never dispose a capture while holding _gate: NAudio joins its
        // capture thread, which may be waiting for _gate in OnDataAvailable.
        DisposeCapture(stopped);
    }

    private void StartCaptureLocked()
    {
        try
        {
            var capture = new WasapiLoopbackCapture();
            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnRecordingStopped;
            _capture = capture;
            Array.Clear(_history);
            _historyWrite = 0;
            _historyFilled = 0;
            capture.StartRecording();
            WatchDefaultDeviceLocked();
            Status = "波形を取得中（音声は保存しません）";
        }
        catch (Exception exception)
        {
            var failed = DetachCaptureLocked("波形を開始できません: " + exception.Message);
            _ = Task.Run(() => DisposeCapture(failed));
        }
    }

    private WasapiLoopbackCapture? DetachCaptureLocked(string status)
    {
        var capture = _capture;
        _capture = null;
        if (capture is not null)
        {
            capture.DataAvailable -= OnDataAvailable;
            capture.RecordingStopped -= OnRecordingStopped;
        }
        UnwatchDefaultDeviceLocked();
        Array.Clear(_smoothed);
        Status = status;
        return capture;
    }

    private static void DisposeCapture(WasapiLoopbackCapture? capture)
    {
        if (capture is null) return;
        try { capture.StopRecording(); } catch (Exception) { }
        try { capture.Dispose(); } catch (Exception) { }
    }

    // The loopback capture is bound to the device that was the default when
    // it started. Follow the user's default-output changes (headset plugged
    // in, output switched) by restarting on the new device.
    private void WatchDefaultDeviceLocked()
    {
        if (_deviceEnumerator is not null) return;
        try
        {
            _deviceEnumerator = new MMDeviceEnumerator();
            _deviceListener = new DeviceChangeListener(() => QueueRestart(400));
            _deviceEnumerator.RegisterEndpointNotificationCallback(_deviceListener);
        }
        catch (Exception)
        {
            UnwatchDefaultDeviceLocked();
        }
    }

    private void UnwatchDefaultDeviceLocked()
    {
        var enumerator = _deviceEnumerator;
        var listener = _deviceListener;
        _deviceEnumerator = null;
        _deviceListener = null;
        if (enumerator is null) return;
        try { if (listener is not null) enumerator.UnregisterEndpointNotificationCallback(listener); } catch (Exception) { }
        try { enumerator.Dispose(); } catch (Exception) { }
    }

    private void QueueRestart(int delayMs)
    {
        // Core Audio forbids blocking inside notification callbacks, and one
        // switch raises several notifications, so restart once, off-thread.
        if (Interlocked.Exchange(ref _restartQueued, 1) != 0) return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            Interlocked.Exchange(ref _restartQueued, 0);
            WasapiLoopbackCapture? stopped;
            lock (_gate)
            {
                if (_disposed || !_wanted) return;
                stopped = DetachCaptureLocked("出力デバイスの切替に追従しています");
            }
            DisposeCapture(stopped);
            lock (_gate)
            {
                if (!_disposed && _wanted && _capture is null) StartCaptureLocked();
            }
        });
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs args)
    {
        WasapiLoopbackCapture? stopped = null;
        lock (_gate)
        {
            if (!ReferenceEquals(sender, _capture)) return;
            stopped = DetachCaptureLocked(args.Exception is null ? "波形は停止中" : "波形が停止しました: " + args.Exception.Message);
        }
        // This callback runs on the capture thread; dispose elsewhere.
        _ = Task.Run(() => DisposeCapture(stopped));
        // A device that disappears mid-capture stops with an exception.
        if (args.Exception is not null) QueueRestart(1500);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        if (args.BytesRecorded <= 0) return;
        float[] bars;
        lock (_gate)
        {
            if (!ReferenceEquals(sender, _capture) || _capture is null) return;
            var format = _capture.WaveFormat;
            AppendMonoSamples(args.Buffer, args.BytesRecorded, format);

            var now = Environment.TickCount64;
            if (now - _lastPublishTicks < PublishIntervalMs || _historyFilled < FftSize / 2) return;
            _lastPublishTicks = now;
            bars = CalculateBars(format.SampleRate);
            for (var index = 0; index < bars.Length; index++)
            {
                var falloff = _smoothed[index] * 0.72f;
                _smoothed[index] = Math.Max(bars[index], falloff);
                bars[index] = MathF.Round(_smoothed[index], 3);
            }
        }

        var listeners = SpectrumChanged;
        if (listeners is null) return;
        // Drop the frame if the previous one is still being delivered.
        if (Interlocked.CompareExchange(ref _publishing, 1, 0) != 0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                var state = new AudioSpectrumState(bars);
                foreach (var listener in listeners.GetInvocationList().Cast<Func<AudioSpectrumState, Task>>())
                {
                    try { await listener(state); }
                    catch (Exception) { }
                }
            }
            finally
            {
                Volatile.Write(ref _publishing, 0);
            }
        });
    }

    private void AppendMonoSamples(byte[] buffer, int byteCount, WaveFormat format)
    {
        var bytesPerSample = Math.Max(1, format.BitsPerSample / 8);
        var channels = Math.Max(1, format.Channels);
        var frameSize = bytesPerSample * channels;
        var frames = byteCount / frameSize;
        // WASAPI shared-mode mix formats are 32-bit float; NAudio normally
        // reports them as IeeeFloat, but accept a raw Extensible header too.
        var isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat ||
                      (format.Encoding == WaveFormatEncoding.Extensible && format.BitsPerSample == 32);
        for (var frame = 0; frame < frames; frame++)
        {
            var sum = 0f;
            for (var channel = 0; channel < channels; channel++)
                sum += ReadSample(buffer, frame * frameSize + channel * bytesPerSample, bytesPerSample, isFloat);
            _history[_historyWrite] = sum / channels;
            _historyWrite = (_historyWrite + 1) & (FftSize - 1);
        }
        _historyFilled = Math.Min(FftSize, _historyFilled + frames);
    }

    private static float ReadSample(byte[] buffer, int offset, int bytesPerSample, bool isFloat)
    {
        if (isFloat && bytesPerSample == 4) return BitConverter.ToSingle(buffer, offset);
        if (bytesPerSample == 2) return BitConverter.ToInt16(buffer, offset) / 32768f;
        if (bytesPerSample == 3)
        {
            var value = buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16;
            if ((value & 0x800000) != 0) value |= unchecked((int)0xff000000);
            return value / 8388608f;
        }
        if (bytesPerSample == 4) return BitConverter.ToInt32(buffer, offset) / 2147483648f;
        return (buffer[offset] - 128) / 128f;
    }

    private float[] CalculateBars(int sampleRate)
    {
        // Oldest → newest samples of the ring buffer, Hann-windowed.
        for (var index = 0; index < FftSize; index++)
        {
            var sample = _history[(_historyWrite + index) & (FftSize - 1)];
            _fft[index].X = sample * Window[index];
            _fft[index].Y = 0;
        }
        FastFourierTransform.FFT(true, FftExponent, _fft);

        var result = new float[_barCount];
        var nyquist = sampleRate / 2.0;
        var top = Math.Min(MaximumFrequency, nyquist * 0.95);
        var ratio = top / MinimumFrequency;
        var binWidth = (double)sampleRate / FftSize;
        for (var bar = 0; bar < _barCount; bar++)
        {
            // Logarithmic bands so bass, mids and treble each get bars.
            var low = MinimumFrequency * Math.Pow(ratio, (double)bar / _barCount);
            var high = MinimumFrequency * Math.Pow(ratio, (double)(bar + 1) / _barCount);
            var first = Math.Clamp((int)Math.Floor(low / binWidth), 1, FftSize / 2 - 1);
            var last = Math.Clamp((int)Math.Ceiling(high / binWidth), first, FftSize / 2 - 1);
            var peak = 0.0;
            for (var bin = first; bin <= last; bin++)
            {
                var magnitude = Math.Sqrt(_fft[bin].X * _fft[bin].X + _fft[bin].Y * _fft[bin].Y);
                if (magnitude > peak) peak = magnitude;
            }
            // NAudio scales the forward FFT by 1/N and the Hann window halves
            // the amplitude, so ×4 maps a full-scale sine to 0 dB. A gentle
            // treble tilt keeps typical music from looking bass-only.
            var centre = Math.Sqrt(low * high);
            var tilt = 2.5 * Math.Log2(centre / 1000.0);
            var decibels = 20 * Math.Log10(peak * 4 * _sensitivity + 1e-9) + tilt;
            result[bar] = (float)Math.Clamp((decibels - FloorDecibels) / -FloorDecibels, 0, 1);
        }
        return result;
    }

    private static float[] CreateWindow()
    {
        var window = new float[FftSize];
        for (var index = 0; index < FftSize; index++)
            window[index] = (float)FastFourierTransform.HannWindow(index, FftSize);
        return window;
    }

    public void Dispose()
    {
        WasapiLoopbackCapture? stopped;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _wanted = false;
            stopped = DetachCaptureLocked("波形は停止中");
        }
        DisposeCapture(stopped);
    }

    private sealed class DeviceChangeListener(Action defaultRenderDeviceChanged) : IMMNotificationClient
    {
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            // WasapiLoopbackCapture uses the Render/Multimedia default.
            if (flow == DataFlow.Render && role == Role.Multimedia) defaultRenderDeviceChanged();
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
