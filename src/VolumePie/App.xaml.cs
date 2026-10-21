using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using VolumePie.Models;
using VolumePie.Services;
using VolumePie.Windows;

namespace VolumePie;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showEventHandle;

    private SettingsService? _settingsService;
    private AudioMixerService? _audioService;
    private BrightnessService? _brightnessService;
    private GlobalInputHookService? _hookService;

    private PieOverlayWindow? _pieWindow;
    private SettingsWindow? _settingsWindow;
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "VolumePie_SingleInstance_Mutex_v1", out bool createdNew);
        _showEventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "VolumePie_ShowWindow_Event_v1");

        if (!createdNew)
        {
            // Another instance is already running (e.g. resident in task tray).
            // Signal the running instance to pop up its Settings Window, then exit immediately.
            _showEventHandle.Set();
            Shutdown();
            return;
        }

        // Listen for activation requests from subsequent instances
        ThreadPool.RegisterWaitForSingleObject(
            _showEventHandle,
            (_, _) => Dispatcher.BeginInvoke(ShowSettingsWindow),
            null,
            -1,
            false);

        _settingsService = new SettingsService();
        LocalizationService.ApplyLanguage(_settingsService.Current.Language);
        LocalizationService.LanguageChanged += () => RebuildTrayContextMenu();
        _audioService = new AudioMixerService();
        _brightnessService = new BrightnessService
        {
            EnableSoftwareFallback = _settingsService.Current.EnableSoftwareDimmerFallback
        };

        _pieWindow = new PieOverlayWindow(
            _settingsService,
            _audioService,
            _brightnessService,
            ShowSettingsWindow);

        _hookService = new GlobalInputHookService(_settingsService.Current);
        _hookService.TriggerActivated += isMouse => _pieWindow?.OnTriggerActivated(isMouse);
        _hookService.TriggerReleased += isMouse => _pieWindow?.OnTriggerReleased(isMouse);
        _hookService.GlobalMouseDown += (x, y) => _pieWindow?.OnGlobalMouseDown(x, y);
        _hookService.Start();

        _settingsService.SettingsChanged += s =>
        {
            _hookService?.UpdateSettings(s);
            UpdateTrayMenuChecks();
        };

        InitializeTrayIcon();

        bool startMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        if (!startMinimized)
        {
            ShowSettingsWindow();
        }
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "VolumePie",
            Visible = true
        };

        try
        {
            var streamInfo = GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
            if (streamInfo != null)
            {
                _trayIcon.Icon = new System.Drawing.Icon(streamInfo.Stream);
            }
            else
            {
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }
        }
        catch
        {
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left)
            {
                Dispatcher.BeginInvoke(() => _pieWindow?.ShowAtCursor(isTriggerHeld: false));
            }
        };

        _trayIcon.DoubleClick += (_, _) => Dispatcher.BeginInvoke(ShowSettingsWindow);

        RebuildTrayContextMenu();
    }

    private void RebuildTrayContextMenu()
    {
        if (_trayIcon == null || _settingsService == null) return;

        _trayIcon.Text = "VolumePie";

        var palette = ThemeService.GetPalette(_settingsService.Current.ThemeMode, _settingsService.Current.ColorPreset);
        bool isDark = !palette.IsLight;

        var menu = new ModernContextMenuStrip(isDark)
        {
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Font = new System.Drawing.Font("Segoe UI", 9.5f),
            Renderer = new ModernTrayMenuRenderer(isDark),
            BackColor = isDark ? System.Drawing.Color.FromArgb(32, 32, 32) : System.Drawing.Color.FromArgb(249, 249, 249),
            DropShadowEnabled = false,
            Padding = new System.Windows.Forms.Padding(3, 5, 3, 5)
        };

        var openPieItem = new System.Windows.Forms.ToolStripMenuItem(LocalizationService.Get("Loc_TrayOpenPie"))
        {
            Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold),
            Padding = new System.Windows.Forms.Padding(12, 6, 12, 6)
        };
        openPieItem.Click += (_, _) => Dispatcher.BeginInvoke(() => _pieWindow?.ShowAtCursor(isTriggerHeld: false));
        menu.Items.Add(openPieItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var outputDevSubMenu = new System.Windows.Forms.ToolStripMenuItem(LocalizationService.Get("Loc_TrayOutputDevice"))
        {
            Padding = new System.Windows.Forms.Padding(12, 6, 12, 6)
        };
        if (outputDevSubMenu.DropDown is System.Windows.Forms.ToolStripDropDownMenu ddm)
        {
            ddm.ShowImageMargin = true;
            ddm.ShowCheckMargin = false;
        }
        outputDevSubMenu.DropDown.Font = new System.Drawing.Font("Segoe UI", 9.5f);
        outputDevSubMenu.DropDown.Renderer = menu.Renderer;
        outputDevSubMenu.DropDown.BackColor = isDark ? System.Drawing.Color.FromArgb(32, 32, 32) : System.Drawing.Color.FromArgb(249, 249, 249);
        ModernContextMenuStrip.ApplySubMenuAttributes(outputDevSubMenu.DropDown, isDark);

        menu.Opening += (_, _) =>
        {
            outputDevSubMenu.DropDownItems.Clear();
            if (_audioService == null) return;
            var devices = _audioService.GetOutputDevices();
            foreach (var dev in devices)
            {
                var devItem = new System.Windows.Forms.ToolStripMenuItem(dev.Name)
                {
                    Checked = dev.IsDefault,
                    Padding = new System.Windows.Forms.Padding(8, 6, 12, 6)
                };
                string targetId = dev.Id;
                devItem.Click += (_, _) =>
                {
                    _audioService.SetDefaultOutputDevice(targetId);
                };
                outputDevSubMenu.DropDownItems.Add(devItem);
            }
        };
        menu.Items.Add(outputDevSubMenu);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var settingsItem = new System.Windows.Forms.ToolStripMenuItem(LocalizationService.Get("Loc_TraySettings"))
        {
            Padding = new System.Windows.Forms.Padding(12, 6, 12, 6)
        };
        settingsItem.Click += (_, _) => Dispatcher.BeginInvoke(ShowSettingsWindow);
        menu.Items.Add(settingsItem);

        var exitItem = new System.Windows.Forms.ToolStripMenuItem(LocalizationService.Get("Loc_TrayExit"))
        {
            Padding = new System.Windows.Forms.Padding(12, 6, 12, 6)
        };
        exitItem.Click += (_, _) => Dispatcher.BeginInvoke(ExitApplication);
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
    }

    private void UpdateTrayMenuChecks()
    {
        RebuildTrayContextMenu();
    }

    private void ShowSettingsWindow()
    {
        if (_settingsService == null || _hookService == null) return;

        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(
                _settingsService,
                _hookService,
                () => _pieWindow?.ShowAtCursor(isTriggerHeld: false));
            MainWindow = _settingsWindow;
            _settingsWindow.Show();
        }
        else
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
                _settingsWindow.WindowState = WindowState.Normal;
        }

        // Force window to front even if invoked from background or second instance
        _settingsWindow.Activate();
        _settingsWindow.Topmost = true;
        _settingsWindow.Topmost = false;
        _settingsWindow.Focus();
    }

    private void ExitApplication()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _hookService?.Dispose();
        _brightnessService?.Dispose();
        _pieWindow?.Close();
        _settingsWindow?.Close();

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _hookService?.Dispose();
        _brightnessService?.Dispose();
        _singleInstanceMutex?.Dispose();
        _showEventHandle?.Dispose();
        base.OnExit(e);
    }
}

internal sealed class ModernContextMenuStrip : System.Windows.Forms.ContextMenuStrip
{
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWCP_ROUND = 2;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly bool _isDark;

    public ModernContextMenuStrip(bool isDark)
    {
        _isDark = isDark;
        DropShadowEnabled = false;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyDwmAttributes(Handle, _isDark);
    }

    protected override void OnOpening(System.ComponentModel.CancelEventArgs e)
    {
        base.OnOpening(e);
        if (IsHandleCreated)
        {
            ApplyDwmAttributes(Handle, _isDark);
        }

        foreach (System.Windows.Forms.ToolStripItem item in Items)
        {
            if (item is System.Windows.Forms.ToolStripMenuItem menuItem && menuItem.HasDropDownItems)
            {
                ApplySubMenuAttributes(menuItem.DropDown, _isDark);
            }
        }
    }

    public static void ApplySubMenuAttributes(System.Windows.Forms.ToolStripDropDown dropDown, bool isDark)
    {
        if (dropDown == null) return;
        dropDown.DropShadowEnabled = false;
        dropDown.Padding = new System.Windows.Forms.Padding(3, 5, 3, 5);
        dropDown.HandleCreated += (_, _) => ApplyDwmAttributes(dropDown.Handle, isDark);
        dropDown.Opening += (_, _) =>
        {
            _ = dropDown.Handle;
            ApplyDwmAttributes(dropDown.Handle, isDark);
        };
        if (dropDown.IsHandleCreated)
        {
            ApplyDwmAttributes(dropDown.Handle, isDark);
        }
    }

    private static void ApplyDwmAttributes(IntPtr hwnd, bool isDark)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            int round = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

            int dark = isDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        }
        catch
        {
            // Silently ignore if not supported by OS
        }
    }
}

internal sealed class ModernTrayMenuColorTable : System.Windows.Forms.ProfessionalColorTable
{
    private readonly bool _isDark;

    public ModernTrayMenuColorTable(bool isDark)
    {
        _isDark = isDark;
        UseSystemColors = false;
    }

    public override System.Drawing.Color ToolStripDropDownBackground =>
        _isDark ? System.Drawing.Color.FromArgb(32, 32, 32) : System.Drawing.Color.FromArgb(249, 249, 249);

    public override System.Drawing.Color MenuBorder =>
        _isDark ? System.Drawing.Color.FromArgb(55, 55, 55) : System.Drawing.Color.FromArgb(218, 218, 218);

    public override System.Drawing.Color MenuItemBorder =>
        System.Drawing.Color.Transparent;

    public override System.Drawing.Color MenuItemSelected =>
        System.Drawing.Color.Transparent;

    public override System.Drawing.Color MenuItemSelectedGradientBegin =>
        System.Drawing.Color.Transparent;

    public override System.Drawing.Color MenuItemSelectedGradientEnd =>
        System.Drawing.Color.Transparent;

    public override System.Drawing.Color MenuItemPressedGradientBegin =>
        System.Drawing.Color.Transparent;

    public override System.Drawing.Color MenuItemPressedGradientMiddle =>
        System.Drawing.Color.Transparent;

    public override System.Drawing.Color MenuItemPressedGradientEnd =>
        System.Drawing.Color.Transparent;

    public override System.Drawing.Color ImageMarginGradientBegin =>
        ToolStripDropDownBackground;

    public override System.Drawing.Color ImageMarginGradientMiddle =>
        ToolStripDropDownBackground;

    public override System.Drawing.Color ImageMarginGradientEnd =>
        ToolStripDropDownBackground;

    public override System.Drawing.Color ImageMarginRevealedGradientBegin =>
        ToolStripDropDownBackground;

    public override System.Drawing.Color ImageMarginRevealedGradientMiddle =>
        ToolStripDropDownBackground;

    public override System.Drawing.Color ImageMarginRevealedGradientEnd =>
        ToolStripDropDownBackground;

    public override System.Drawing.Color SeparatorDark =>
        _isDark ? System.Drawing.Color.FromArgb(52, 52, 52) : System.Drawing.Color.FromArgb(226, 226, 226);

    public override System.Drawing.Color SeparatorLight =>
        System.Drawing.Color.Transparent;

    public override System.Drawing.Color CheckBackground =>
        System.Drawing.Color.Transparent;

    public override System.Drawing.Color CheckSelectedBackground =>
        System.Drawing.Color.Transparent;

    public override System.Drawing.Color CheckPressedBackground =>
        System.Drawing.Color.Transparent;

    public override System.Drawing.Color ButtonSelectedBorder =>
        System.Drawing.Color.Transparent;
}

internal sealed class ModernTrayMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
{
    private readonly bool _isDark;

    public ModernTrayMenuRenderer(bool isDark) : base(new ModernTrayMenuColorTable(isDark))
    {
        _isDark = isDark;
    }

    protected override void OnRenderMenuItemBackground(System.Windows.Forms.ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            var rect = new System.Drawing.Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
            using var brush = new System.Drawing.SolidBrush(
                _isDark ? System.Drawing.Color.FromArgb(55, 55, 55) : System.Drawing.Color.FromArgb(232, 232, 232));
            using var path = CreateRoundedRectanglePath(rect, 4);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
        }
    }

    protected override void OnRenderItemText(System.Windows.Forms.ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item == null)
        {
            base.OnRenderItemText(e);
            return;
        }

        var textColor = !e.Item.Enabled
            ? (_isDark ? System.Drawing.Color.FromArgb(120, 120, 120) : System.Drawing.Color.FromArgb(160, 160, 160))
            : (_isDark ? System.Drawing.Color.FromArgb(240, 240, 240) : System.Drawing.Color.FromArgb(28, 28, 28));

        int textX = e.TextRectangle.X;
        int rightMargin = (e.Item is System.Windows.Forms.ToolStripMenuItem mi && mi.HasDropDownItems) ? 24 : 12;
        int textW = Math.Max(e.TextRectangle.Width, e.Item.Width - textX - rightMargin);
        var fullRect = new System.Drawing.Rectangle(textX, 0, textW, e.Item.Height);

        var flags = System.Windows.Forms.TextFormatFlags.Left |
                    System.Windows.Forms.TextFormatFlags.VerticalCenter |
                    System.Windows.Forms.TextFormatFlags.SingleLine |
                    System.Windows.Forms.TextFormatFlags.NoPrefix;
        System.Windows.Forms.TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, fullRect, textColor, flags);
    }

    protected override void OnRenderArrow(System.Windows.Forms.ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = _isDark
            ? System.Drawing.Color.FromArgb(195, 195, 195)
            : System.Drawing.Color.FromArgb(70, 70, 70);

        int arrowH = e.ArrowRectangle.Height;
        int itemH = e.Item?.Height ?? arrowH;
        int arrowY = (itemH - arrowH) / 2;
        e.ArrowRectangle = new System.Drawing.Rectangle(e.ArrowRectangle.X, arrowY, e.ArrowRectangle.Width, arrowH);
        base.OnRenderArrow(e);
    }

    protected override void OnRenderSeparator(System.Windows.Forms.ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new System.Drawing.Pen(
            _isDark ? System.Drawing.Color.FromArgb(52, 52, 52) : System.Drawing.Color.FromArgb(226, 226, 226),
            1.0f);
        e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
    }

    protected override void OnRenderItemCheck(System.Windows.Forms.ToolStripItemImageRenderEventArgs e)
    {
        var rect = e.ImageRectangle;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var pen = new System.Drawing.Pen(
            _isDark ? System.Drawing.Color.FromArgb(56, 189, 248) : System.Drawing.Color.FromArgb(0, 120, 215),
            2.0f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };

        int cx = rect.X + rect.Width / 2;
        int cy = (e.Item?.Height ?? rect.Height) / 2;
        g.DrawLines(pen, new[]
        {
            new System.Drawing.Point(cx - 4, cy),
            new System.Drawing.Point(cx - 1, cy + 3),
            new System.Drawing.Point(cx + 4, cy - 3)
        });
    }

    protected override void OnRenderToolStripBorder(System.Windows.Forms.ToolStripRenderEventArgs e)
    {
        using var pen = new System.Drawing.Pen(
            _isDark ? System.Drawing.Color.FromArgb(55, 55, 55) : System.Drawing.Color.FromArgb(218, 218, 218),
            1.0f);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(System.Drawing.Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}