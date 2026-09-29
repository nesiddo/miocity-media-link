using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;

namespace MioCity.LocalMediaBridge;

/// <summary>
/// Friendly first-run/settings window for the MioCity-only local bridge.
/// During normal use it stays quietly in the Windows notification area.
/// </summary>
public sealed class BridgeForm : Form
{
    private const string StartupTaskId = "MioCityLocalMediaBridgeStartup";
    private static readonly Color WindowBackground = Color.FromArgb(242, 247, 250);
    private static readonly Color CardBackground = Color.White;
    private static readonly Color BrandColor = Color.FromArgb(14, 169, 205);
    private static readonly Color TextColor = Color.FromArgb(31, 49, 62);
    private static readonly Color MutedColor = Color.FromArgb(92, 113, 127);

    private readonly BridgeRuntime _runtime;
    private readonly bool _startHidden;
    private readonly Label _bridgeStatus = new() { AutoSize = true, MaximumSize = new Size(560, 0) };
    private readonly Button _startupButton;
    private readonly Button _exitButton;
    private readonly NotifyIcon _trayIcon;
    // Status is refreshed only while the window is visible, so the tray-only
    // state costs nothing.
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 2000 };
    private StartupTask? _startupTask;
    private string? _startError;
    private string? _startupNote;
    private bool _exitRequested;
    private bool _closing;

    public BridgeForm(bool startHidden)
    {
        _startHidden = startHidden;
        _runtime = new BridgeRuntime(BridgeSettings.LoadOrCreate());

        Text = "MioCity Media Link";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(700, 520);
        ClientSize = new Size(700, 520);
        BackColor = WindowBackground;
        ForeColor = TextColor;
        Font = new Font("Yu Gothic UI", 10f);
        Icon = CreateBrandIcon();
        MaximizeBox = false;
        if (startHidden)
        {
            // Application.Run must show the form once; keep that first frame
            // invisible so a login auto-start never flashes the window.
            Opacity = 0;
            ShowInTaskbar = false;
        }

        var brand = new FlowLayoutPanel
        {
            AutoSize = true,
            MaximumSize = new Size(620, 0),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8),
        };
        brand.Controls.Add(new Label
        {
            Text = "M",
            Size = new Size(54, 54),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = BrandColor,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 24f, FontStyle.Bold),
            Margin = new Padding(0, 0, 14, 0),
        });
        var brandText = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 2, 0, 0),
        };
        brandText.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "MioCity Media Link",
            ForeColor = TextColor,
            Font = new Font("Yu Gothic UI", 18f, FontStyle.Bold),
            Margin = Padding.Empty,
        });
        brandText.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "MIOCITY専用・ゲーム内メディア連携ツール",
            ForeColor = BrandColor,
            Font = new Font("Yu Gothic UI", 9f, FontStyle.Bold),
            Margin = new Padding(1, 1, 0, 0),
        });
        brand.Controls.Add(brandText);

        var welcomeCard = CreateCard();
        welcomeCard.Controls.Add(CreateHeading("音楽を、ゲームの中でも快適に。"));
        welcomeCard.Controls.Add(CreateBody(
            "このアプリはMioCityのFiveM UIに、Windowsで再生中の曲名・アーティスト・ジャケットを表示するための専用ツールです。音楽アプリやブラウザなど、Windowsのメディア機能に対応したアプリで利用できます。"));

        var safetyCard = CreateCard(Color.FromArgb(232, 248, 252));
        safetyCard.Controls.Add(CreateHeading("✓  このPCの中だけで安全に接続"));
        safetyCard.Controls.Add(CreateBody(
            "接続は 127.0.0.1 のみを使用します。メディア・波形・マップ情報をMioCityサーバーへ送信しません。FiveMやGTA Vのメモリ・ファイルにもアクセスせず、FiveM未接続時は自動休止します。"));

        var statusCard = CreateCard();
        statusCard.Controls.Add(CreateHeading("接続状態"));
        _bridgeStatus.Text = "● MioCityとの接続を準備しています…";
        _bridgeStatus.ForeColor = MutedColor;
        _bridgeStatus.Font = new Font("Yu Gothic UI", 10f, FontStyle.Bold);
        _bridgeStatus.Margin = new Padding(0, 4, 0, 0);
        statusCard.Controls.Add(_bridgeStatus);

        var openButton = CreateButton("接続案内ページを開く", true);
        openButton.Click += (_, _) => Process.Start(new ProcessStartInfo($"http://127.0.0.1:{BridgeRuntime.Port}/") { UseShellExecute = true });
        var mapButton = CreateButton("セカンドモニターマップ");
        mapButton.Click += (_, _) => Process.Start(new ProcessStartInfo(
            $"http://127.0.0.1:{BridgeRuntime.Port}/map?token={_runtime.Settings.MapViewerToken}") { UseShellExecute = true });
        _startupButton = CreateButton("Windows起動時: 確認中…");
        _startupButton.Click += ToggleStartupAsync;
        _exitButton = CreateButton("アプリを終了");
        _exitButton.Click += ExitBridgeAsync;

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            MaximumSize = new Size(620, 0),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 4, 0, 0),
        };
        actions.Controls.Add(openButton);
        actions.Controls.Add(mapButton);
        actions.Controls.Add(_startupButton);
        actions.Controls.Add(_exitButton);

        var trayHint = CreateBody("右上の×ボタンでは終了せず、画面だけを閉じて通知領域に常駐します。もう一度開くときは通知領域のMioCityアイコンをダブルクリックしてください。");
        trayHint.Margin = new Padding(0, 2, 0, 0);

        var credit = new Label
        {
            AutoSize = true,
            Text = "MioCity exclusive tool  •  Created by nesiddo",
            ForeColor = Color.FromArgb(120, 139, 151),
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            Margin = new Padding(0, 10, 0, 0),
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(38, 30, 38, 26),
            BackColor = WindowBackground,
        };
        foreach (var control in new Control[] { brand, welcomeCard, safetyCard, statusCard, actions, trayHint, credit })
            layout.Controls.Add(control);
        Controls.Add(layout);

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("MioCity Media Linkを開く", null, (_, _) => ShowFromTray());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("アプリを終了", null, async (_, _) => await ExitBridgeCoreAsync());
        _trayIcon = new NotifyIcon
        {
            Icon = Icon,
            Text = "MioCity Media Link",
            ContextMenuStrip = trayMenu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();

        _statusTimer.Tick += (_, _) => RefreshBridgeStatus();
        VisibleChanged += (_, _) =>
        {
            if (Visible && Opacity > 0) { RefreshBridgeStatus(); _statusTimer.Start(); }
            else _statusTimer.Stop();
        };

        Load += StartBridgeAsync;
        Shown += (_, _) => { if (_startHidden) BeginInvoke((Action)HideToTray); };
        FormClosing += HandleFormClosing;
    }

    private static FlowLayoutPanel CreateCard(Color? background = null) => new()
    {
        AutoSize = true,
        MinimumSize = new Size(620, 0),
        MaximumSize = new Size(620, 0),
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        Padding = new Padding(18, 15, 18, 15),
        Margin = new Padding(0, 0, 0, 10),
        BackColor = background ?? CardBackground,
    };

    private static Label CreateHeading(string text) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(580, 0),
        Text = text,
        ForeColor = TextColor,
        Font = new Font("Yu Gothic UI", 11f, FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 5),
    };

    private static Label CreateBody(string text) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(580, 0),
        Text = text,
        ForeColor = MutedColor,
        Font = new Font("Yu Gothic UI", 9.5f),
        Margin = Padding.Empty,
    };

    private static Button CreateButton(string text, bool primary = false)
    {
        var button = new Button
        {
            AutoSize = true,
            MinimumSize = new Size(0, 38),
            Text = text,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? BrandColor : Color.White,
            ForeColor = primary ? Color.White : TextColor,
            Padding = new Padding(12, 4, 12, 4),
            Margin = new Padding(0, 0, 8, 6),
            Font = new Font("Yu Gothic UI", 9f, FontStyle.Bold),
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderColor = primary ? BrandColor : Color.FromArgb(196, 211, 220);
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private static Icon CreateBrandIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var background = new SolidBrush(BrandColor))
        using (var foreground = new SolidBrush(Color.White))
        using (var font = new Font("Segoe UI", 21f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var alignment = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.FillRectangle(background, new Rectangle(1, 1, 30, 30));
            graphics.DrawString("M", font, foreground, new RectangleF(0, -1, 32, 32), alignment);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    private async void StartBridgeAsync(object? sender, EventArgs args)
    {
        try
        {
            await _runtime.StartAsync();
        }
        catch (Exception exception)
        {
            // Typically another program already uses the loopback port.
            _startError = exception.Message;
        }
        if (_closing) return;
        RefreshBridgeStatus();
        await RefreshStartupButtonAsync();
    }

    private void RefreshBridgeStatus()
    {
        if (_closing || IsDisposed) return;
        if (_startError is not null)
        {
            _bridgeStatus.ForeColor = Color.FromArgb(205, 69, 89);
            _bridgeStatus.Text = $"● 起動できませんでした（127.0.0.1:{BridgeRuntime.Port} が使用中の可能性）: " + _startError;
            return;
        }
        if (!_runtime.IsStarted) return;

        var connected = _runtime.ConnectedClientCount > 0;
        _bridgeStatus.ForeColor = connected ? Color.FromArgb(20, 145, 103) : MutedColor;
        var lines = new List<string>
        {
            connected
                ? "● FiveM（MioCity）と接続中"
                : "● FiveMの接続を待っています（未接続の間は自動休止）",
            _runtime.Status,
            _runtime.SpectrumStatus,
        };
        if (_runtime.MapViewerCount > 0) lines.Add($"セカンドモニターマップ: {_runtime.MapViewerCount} 画面で表示中");
        lines.Add($"待受: 127.0.0.1:{BridgeRuntime.Port}（このPC内のみ）");
        if (_startupNote is not null) lines.Add(_startupNote);
        var text = string.Join(Environment.NewLine, lines);
        if (_bridgeStatus.Text != text) _bridgeStatus.Text = text;
    }

    private async Task RefreshStartupButtonAsync()
    {
        if (!HasPackageIdentity())
        {
            _startupButton.Text = "自動起動はMSIX版のみ";
            _startupButton.Enabled = false;
            return;
        }

        try
        {
            _startupTask ??= await StartupTask.GetAsync(StartupTaskId);
            if (_closing) return;
            _startupButton.Enabled = _startupTask.State is not (StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy);
            _startupButton.Text = _startupTask.State switch
            {
                StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => "Windows起動時: ON",
                StartupTaskState.DisabledByUser => "Windows起動時: OFF（設定を開く）",
                StartupTaskState.DisabledByPolicy => "Windows起動時: 管理者により無効",
                _ => "Windows起動時: OFF",
            };
        }
        catch (Exception exception)
        {
            if (_closing) return;
            _startupButton.Text = "自動起動を利用できません";
            _startupButton.Enabled = false;
            _startupNote = "自動起動: " + exception.Message;
            RefreshBridgeStatus();
        }
    }

    private async void ToggleStartupAsync(object? sender, EventArgs args)
    {
        if (_startupTask is null || _closing) return;
        _startupButton.Enabled = false;
        try
        {
            if (_startupTask.State == StartupTaskState.Enabled)
                _startupTask.Disable();
            else if (_startupTask.State == StartupTaskState.DisabledByUser)
                Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true });
            else
                await _startupTask.RequestEnableAsync();
        }
        catch (Exception exception)
        {
            _startupNote = "自動起動の変更に失敗しました: " + exception.Message;
            RefreshBridgeStatus();
        }
        if (!_closing) await RefreshStartupButtonAsync();
    }

    private static bool HasPackageIdentity()
    {
        try
        {
            _ = Package.Current.Id.FamilyName;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void HideToTray()
    {
        if (_closing || IsDisposed) return;
        ShowInTaskbar = false;
        Hide();
        Opacity = 1;
    }

    public void ShowFromTray()
    {
        if (_closing || IsDisposed || Disposing) return;
        if (InvokeRequired)
        {
            try { BeginInvoke((Action)ShowFromTray); }
            catch (InvalidOperationException) { }
            return;
        }
        Opacity = 1;
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        RefreshBridgeStatus();
        _statusTimer.Start();
        Activate();
        BringToFront();
    }

    private async void ExitBridgeAsync(object? sender, EventArgs args) => await ExitBridgeCoreAsync();

    private async Task ExitBridgeCoreAsync()
    {
        if (_closing) return;
        _closing = true;
        _exitRequested = true;
        _exitButton.Enabled = false;
        _exitButton.Text = "終了しています…";
        _startupButton.Enabled = false;

        // Hide immediately, then stop network services asynchronously. The old
        // implementation synchronously waited here and could deadlock the UI.
        _statusTimer.Stop();
        ShowInTaskbar = false;
        Hide();
        _trayIcon.Visible = false;
        try { await _runtime.DisposeAsync(); }
        catch (Exception) { /* Process exit must remain available. */ }
        _trayIcon.Dispose();
        if (!IsDisposed) Close();
    }

    private void HandleFormClosing(object? sender, FormClosingEventArgs args)
    {
        if (!_exitRequested && args.CloseReason == CloseReason.UserClosing)
        {
            args.Cancel = true;
            // Run after FormClosing returns so Windows never waits on a nested
            // hide/close transition from inside its close callback.
            BeginInvoke((Action)HideToTray);
            return;
        }

        if (!_closing)
        {
            // Windows session shutdown cannot await. Start best-effort cleanup
            // without blocking the UI or delaying system shutdown.
            _closing = true;
            _statusTimer.Stop();
            _trayIcon.Visible = false;
            _ = _runtime.DisposeAsync().AsTask();
        }
    }
}
