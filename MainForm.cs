using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ScannerDisabler;

internal sealed class MainForm : Form
{
    private readonly Color _primary = Color.FromArgb(70, 91, 230);
    private readonly AppSettings _settings = SettingsStore.Load();
    private readonly ComboBox _interfaceSelector;
    private readonly Label _statusTitle;
    private readonly Label _statusDetail;
    private readonly Panel _statusDot;
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly Icon _applicationIcon;
    private readonly Image _mascotImage;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private IReadOnlyList<WirelessInterface> _interfaces = Array.Empty<WirelessInterface>();
    private Guid? _protectedInterfaceId;
    private bool _loadingSelection;
    private bool _busy;
    private bool _exitRequested;
    private bool _hasShownTrayHint;

    private WirelessInterface? SelectedInterface
    {
        get
        {
            if (_interfaceSelector.SelectedItem is not WirelessInterface item) return null;
            return _interfaces.FirstOrDefault(candidate => candidate.Id == item.Id) ?? item;
        }
    }

    public MainForm()
    {
        Text = "Penguin Anti-Scan";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(540, 355);
        BackColor = Color.FromArgb(242, 245, 250);
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;

        _applicationIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? CreateWifiIcon();
        _mascotImage = LoadMascot();
        Icon = _applicationIcon;

        var mascot = new PictureBox
        {
            Image = _mascotImage, SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(25, 18), Size = new Size(78, 78)
        };

        var title = new Label
        {
            AutoSize = true, Text = "Penguin Anti-Scan", Font = new Font("Segoe UI Semibold", 20F),
            ForeColor = Color.FromArgb(27, 34, 48), Location = new Point(111, 24)
        };
        var subtitle = new Label
        {
            AutoSize = true, Text = "Less scanning. More penguin.",
            ForeColor = Color.FromArgb(101, 110, 127), Location = new Point(115, 65)
        };

        var card = new RoundedPanel
        {
            BackColor = Color.White, Location = new Point(30, 108), Size = new Size(480, 202)
        };
        var selectorCaption = new Label
        {
            AutoSize = true, Text = "WI-FI ADAPTER", Font = new Font("Segoe UI Semibold", 8F),
            ForeColor = Color.FromArgb(120, 129, 145), Location = new Point(24, 20)
        };
        _interfaceSelector = new ComboBox
        {
            Location = new Point(20, 45), Size = new Size(440, 48), DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 42, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 11F), BackColor = Color.FromArgb(247, 249, 253),
            ForeColor = Color.FromArgb(33, 40, 55)
        };
        _interfaceSelector.DrawItem += DrawInterfaceItem;
        _interfaceSelector.SelectedIndexChanged += async (_, _) => await SelectionChangedAsync();

        _statusDot = new CirclePanel { Location = new Point(23, 118), Size = new Size(13, 13), BackColor = Color.FromArgb(130, 138, 150) };
        _statusTitle = new Label
        {
            AutoSize = true, Text = "Getting ready…", Font = new Font("Segoe UI Semibold", 12F),
            ForeColor = Color.FromArgb(40, 48, 63), Location = new Point(45, 111)
        };
        _statusDetail = new Label
        {
            AutoSize = false, Size = new Size(430, 48), Text = "Checking the wireless adapter.",
            ForeColor = Color.FromArgb(101, 110, 127), Location = new Point(23, 143)
        };

        card.Controls.Add(selectorCaption);
        card.Controls.Add(_interfaceSelector);
        card.Controls.Add(_statusDot);
        card.Controls.Add(_statusTitle);
        card.Controls.Add(_statusDetail);
        Controls.Add(mascot);
        Controls.Add(title);
        Controls.Add(subtitle);
        Controls.Add(card);

        var footer = new Label
        {
            AutoSize = true, Text = "🐧  Lives in your tray and scares scans away.",
            ForeColor = Color.FromArgb(115, 124, 141), Location = new Point(105, 326)
        };
        Controls.Add(footer);

        _trayMenu = new ContextMenuStrip { Font = new Font("Segoe UI", 9.5F) };
        _trayMenu.Opening += (_, _) => BuildTrayMenu();
        _trayIcon = new NotifyIcon
        {
            Icon = _applicationIcon, Text = "Penguin Anti-Scan", Visible = true, ContextMenuStrip = _trayMenu
        };
        _trayIcon.DoubleClick += (_, _) => RestoreWindow();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _refreshTimer.Tick += async (_, _) => await RefreshAndProtectAsync(rebuildSelector: false);

        Shown += async (_, _) =>
        {
            await RefreshAndProtectAsync(rebuildSelector: true);
            _refreshTimer.Start();
        };
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) HideToTray(); };
        FormClosing += OnFormClosing;
    }

    private async Task RefreshAndProtectAsync(bool rebuildSelector)
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var latest = await Task.Run(WirelessService.GetInterfaces);
            var topologyChanged = latest.Count != _interfaces.Count ||
                                  latest.Select(item => item.Id).Except(_interfaces.Select(item => item.Id)).Any();
            _interfaces = latest;
            if (rebuildSelector || topologyChanged)
                RebuildSelector();

            var selected = SelectedInterface;
            if (selected is null)
            {
                ShowNoAdapter();
                return;
            }

            if (_protectedInterfaceId != selected.Id)
            {
                var autoConfigResult = WirelessService.EnableAutoConfig(selected.Id);
                if (!autoConfigResult.Success)
                {
                    ShowError(autoConfigResult.Message);
                    return;
                }
                _protectedInterfaceId = selected.Id;
            }

            if (selected.Connected && selected.BackgroundScanEnabled != false)
            {
                var result = await Task.Run(() => WirelessService.SetBackgroundScan(selected.Id, false));
                if (!result.Success)
                {
                    ShowError(result.Message);
                    return;
                }
                _interfaces = await Task.Run(WirelessService.GetInterfaces);
                selected = SelectedInterface ?? selected;
            }
            UpdateStatus(selected);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally { SetBusy(false); }
    }

    private void RebuildSelector()
    {
        var wantedId = _settings.SelectedInterfaceId;
        _loadingSelection = true;
        _interfaceSelector.Items.Clear();
        foreach (var wirelessInterface in _interfaces) _interfaceSelector.Items.Add(wirelessInterface);
        var index = -1;
        for (var i = 0; i < _interfaces.Count; i++)
        {
            if (string.Equals(_interfaces[i].Id.ToString(), wantedId, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }
        if (index < 0 && _interfaces.Count > 0) index = 0;
        _interfaceSelector.SelectedIndex = index;
        _loadingSelection = false;
        SaveSelection();
    }

    private async Task SelectionChangedAsync()
    {
        if (_loadingSelection || _busy) return;
        var newSelection = SelectedInterface;
        if (_protectedInterfaceId.HasValue && newSelection?.Id != _protectedInterfaceId.Value)
            await Task.Run(() => WirelessService.SetBackgroundScan(_protectedInterfaceId.Value, true));
        _protectedInterfaceId = null;
        SaveSelection();
        await RefreshAndProtectAsync(rebuildSelector: false);
    }

    private void SaveSelection()
    {
        _settings.SelectedInterfaceId = SelectedInterface?.Id.ToString();
        SettingsStore.Save(_settings);
    }

    private void UpdateStatus(WirelessInterface selected)
    {
        if (!selected.Connected)
        {
            _statusDot.BackColor = Color.FromArgb(232, 163, 54);
            _statusTitle.Text = "The penguin is waiting";
            _statusDetail.Text = "Windows can only block background scans while the adapter is connected. Protection will start automatically after you connect.";
        }
        else if (selected.BackgroundScanEnabled == false)
        {
            _statusDot.BackColor = Color.FromArgb(40, 185, 112);
            _statusTitle.Text = "Penguin on guard";
            _statusDetail.Text = "Periodic scanning is blocked. Windows can still search for networks on demand when you open the Wi-Fi panel.";
        }
        else
        {
            _statusDot.BackColor = Color.FromArgb(204, 70, 80);
            _statusTitle.Text = "The penguin tripped";
            _statusDetail.Text = "The adapter or its driver did not accept the change. The app will retry automatically.";
        }
    }

    private void ShowNoAdapter()
    {
        _statusDot.BackColor = Color.FromArgb(204, 70, 80);
        _statusTitle.Text = "The penguin cannot find Wi-Fi";
        _statusDetail.Text = "Connect or enable a wireless adapter.";
    }

    private void ShowError(string message)
    {
        _statusDot.BackColor = Color.FromArgb(204, 70, 80);
        _statusTitle.Text = "The penguin tripped";
        _statusDetail.Text = message;
    }

    private void DrawInterfaceItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _interfaceSelector.Items.Count ||
            _interfaceSelector.Items[e.Index] is not WirelessInterface wirelessInterface) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var dot = new SolidBrush(selected ? Color.White : _primary);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillEllipse(dot, e.Bounds.Left + 12, e.Bounds.Top + 16, 10, 10);
        using var textBrush = new SolidBrush(selected ? Color.White : Color.FromArgb(33, 40, 55));
        e.Graphics.DrawString(wirelessInterface.Name, _interfaceSelector.Font, textBrush, e.Bounds.Left + 31, e.Bounds.Top + 10);
        e.DrawFocusRectangle();
    }

    private void BuildTrayMenu()
    {
        _trayMenu.Items.Clear();
        _trayMenu.Items.Add(new ToolStripMenuItem("Open Penguin Anti-Scan", null, (_, _) => RestoreWindow())
            { Font = new Font(_trayMenu.Font, FontStyle.Bold) });
        var selected = SelectedInterface;
        _trayMenu.Items.Add(new ToolStripMenuItem(selected is null ? "No Wi-Fi adapter" : $"🐧 Protecting: {selected.Name}") { Enabled = false });
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Refresh", null, async (_, _) => await RefreshAndProtectAsync(rebuildSelector: true));
        _trayMenu.Items.Add("Exit and restore Wi-Fi", null, async (_, _) => await ExitApplicationAsync());
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _interfaceSelector.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_exitRequested) { _trayIcon.Visible = false; return; }
        if (e.CloseReason == CloseReason.WindowsShutDown)
        {
            RestoreBackgroundScan();
            _trayIcon.Visible = false;
            return;
        }
        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        Hide();
        WindowState = FormWindowState.Normal;
        if (_hasShownTrayHint) return;
        _hasShownTrayHint = true;
        _trayIcon.ShowBalloonTip(2500, "The penguin is still on guard", "Periodic Wi-Fi scanning remains blocked from the system tray.", ToolTipIcon.Info);
    }

    private void RestoreWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }

    private async Task ExitApplicationAsync()
    {
        _refreshTimer.Stop();
        await Task.Run(RestoreBackgroundScan);
        _exitRequested = true;
        _trayIcon.Visible = false;
        Close();
    }

    private void RestoreBackgroundScan()
    {
        if (_protectedInterfaceId.HasValue)
            WirelessService.SetBackgroundScan(_protectedInterfaceId.Value, true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Dispose();
            _trayIcon.Dispose();
            _trayMenu.Dispose();
            _applicationIcon.Dispose();
            _mascotImage.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Icon CreateWifiIcon()
    {
        using var bitmap = new Bitmap(64, 64);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using var background = new SolidBrush(Color.FromArgb(70, 91, 230));
            graphics.FillEllipse(background, 2, 2, 60, 60);
            using var pen = new Pen(Color.White, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawArc(pen, 14, 18, 36, 28, 210, 120);
            graphics.DrawArc(pen, 21, 29, 22, 17, 210, 120);
            using var dot = new SolidBrush(Color.White);
            graphics.FillEllipse(dot, 29, 44, 6, 6);
        }
        var handle = bitmap.GetHicon();
        try { using var temporary = Icon.FromHandle(handle); return (Icon)temporary.Clone(); }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool DestroyIcon(IntPtr handle);

    private static Image LoadMascot()
    {
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("ScannerDisabler.Assets.penguin-anti-scan.png")
            ?? throw new InvalidOperationException("The penguin icon could not be found.");
        return new Bitmap(stream);
    }
}

internal sealed class RoundedPanel : Panel
{
    protected override void OnResize(EventArgs eventArgs)
    {
        base.OnResize(eventArgs);
        if (Width <= 0 || Height <= 0) return;
        using var path = new GraphicsPath();
        const int radius = 16;
        path.AddArc(0, 0, radius, radius, 180, 90);
        path.AddArc(Width - radius, 0, radius, radius, 270, 90);
        path.AddArc(Width - radius, Height - radius, radius, radius, 0, 90);
        path.AddArc(0, Height - radius, radius, radius, 90, 90);
        path.CloseFigure();
        var previousRegion = Region;
        Region = new Region(path);
        previousRegion?.Dispose();
    }
}

internal sealed class CirclePanel : Panel
{
    protected override void OnResize(EventArgs eventArgs)
    {
        base.OnResize(eventArgs);
        var previousRegion = Region;
        using var path = new GraphicsPath();
        path.AddEllipse(0, 0, Width, Height);
        Region = new Region(path);
        previousRegion?.Dispose();
    }
}
