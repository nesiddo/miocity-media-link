namespace MioCity.LocalMediaBridge;

internal static class Program
{
    private const string InstanceMutexName = @"Local\MioCity.LocalMediaBridge.Singleton";
    private const string ShowSignalName = @"Local\MioCity.LocalMediaBridge.Show";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        using var instanceMutex = new Mutex(true, InstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            try
            {
                using var existingSignal = EventWaitHandle.OpenExisting(ShowSignalName);
                existingSignal.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The first instance is still starting. It already owns the
                // loopback port, so this second process should simply exit.
            }
            return;
        }

        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        using var shutdownSignal = new ManualResetEvent(false);
        var startHidden = args.Any(argument => string.Equals(argument, "--startup", StringComparison.OrdinalIgnoreCase));
        using var form = new BridgeForm(startHidden);
        _ = form.Handle;

        var signalThread = new Thread(() =>
        {
            var handles = new WaitHandle[] { showSignal, shutdownSignal };
            while (WaitHandle.WaitAny(handles) == 0)
            {
                if (form.IsDisposed || form.Disposing) break;
                try { form.BeginInvoke((Action)form.ShowFromTray); }
                catch (InvalidOperationException) { break; }
            }
        })
        {
            IsBackground = true,
            Name = "Bridge window activation",
        };
        signalThread.Start();

        try { Application.Run(form); }
        finally
        {
            shutdownSignal.Set();
            instanceMutex.ReleaseMutex();
        }
    }
}
