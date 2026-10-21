using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VolumePie.Models;
using VolumePie.Services;

namespace VolumePie.Windows;

public partial class PieOverlayWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly AudioMixerService _audioService;
    private readonly BrightnessService _brightnessService;
    private readonly Action _openSettingsAction;
    private List<AudioOutputDeviceInfo> _cachedDevices = new();
    private DateTime _suppressDeactivateAndEscapeUntilUtc = DateTime.MinValue;
    private DateTime _shownAtUtc = DateTime.MinValue;
    private const double SmartHoldThresholdMs = 400.0;

    private enum PiePage
    {
        Main,
        Apps
    }

    private PiePage _currentPage = PiePage.Main;

    public PieOverlayWindow(
        SettingsService settingsService,
        AudioMixerService audioService,
        BrightnessService brightnessService,
        Action openSettingsAction)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _audioService = audioService;
        _brightnessService = brightnessService;
        _openSettingsAction = openSettingsAction;

        PieCanvas.RequestClose += HideOverlay;
        PieCanvas.RequestGoBack += () => SwitchToPage(PiePage.Main);

        _settingsService.SettingsChanged += ApplySettingsToUi;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            Dispatcher.BeginInvoke(() => ApplySettingsToUi(_settingsService.Current));
        };

        ApplySettingsToUi(_settingsService.Current);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
    }

    public void ApplySettingsToUi(AppSettings settings)
    {
        var palette = ThemeService.GetPalette(settings.ThemeMode, settings.ColorPreset);
        PieCanvas.Palette = palette;
        PieCanvas.SurfaceTexture = settings.SurfaceTexture;
        PieCanvas.LightingEffect = settings.LightingEffect;
        PieCanvas.SliderMode = settings.SliderMode;
        PieCanvas.SliceLabelStyle = settings.SliceLabelStyle;
        PieCanvas.DirectHoverAdjust = settings.DirectHoverAdjust;
        PieCanvas.WheelStep = settings.WheelStep;
        _brightnessService.EnableSoftwareFallback = settings.EnableSoftwareDimmerFallback;
        UpdateChromeVisuals(palette);
    }

    public void OnTriggerActivated(bool isMouseTrigger)
    {
        var settings = _settingsService.Current;

        if (IsVisible)
        {
            // If the user is pressing the mouse trigger button ON a pie slice while the menu is already open,
            // let them drag the slice with that button instead of closing the menu!
            if (isMouseTrigger && PieCanvas.IsInteractingWithSlice)
            {
                PieCanvas.IsTriggerHeld = true;
                return;
            }

            if (settings.TriggerBehavior != TriggerBehaviorMode.HoldOnly)
            {
                HideOverlay();
            }
            return;
        }

        // Only pass isTriggerHeld: true when opened via Mouse Button Trigger so holding the mouse trigger button can drag directly
        ShowAtCursor(isTriggerHeld: isMouseTrigger);
    }

    public void OnTriggerReleased(bool isMouseTrigger)
    {
        if (!IsVisible) return;

        // Immediately stop any active trigger-hold drag so the value locks at the exact release position!
        PieCanvas.IsTriggerHeld = false;
        var behavior = _settingsService.Current.TriggerBehavior;

        if (behavior == TriggerBehaviorMode.HoldOnly)
        {
            HideOverlay();
        }
        else if (behavior == TriggerBehaviorMode.SmartHybrid &&
                 (DateTime.UtcNow - _shownAtUtc).TotalMilliseconds >= SmartHoldThresholdMs)
        {
            // Smart: a quick tap keeps the menu open (toggle), a long hold closes it on release (hold)
            HideOverlay();
        }
    }

    public void ShowAtCursor(bool isTriggerHeld)
    {
        _currentPage = PiePage.Main;
        PieCanvas.IsInSubPage = false;
        OutputDevicePopupBorder.Visibility = Visibility.Collapsed;
        RefreshOutputDeviceBar();
        RefreshPieItems();
        ApplySettingsToUi(_settingsService.Current);

        PieCanvas.ResetInteractionState();
        PieCanvas.IsTriggerHeld = isTriggerHeld;
        _shownAtUtc = DateTime.UtcNow;

        PositionWindowAtCursor();

        if (!IsVisible)
        {
            Show();
        }

        Activate();
        PieCanvas.Focus();

        // Snappy pop-in animation
        var anim = new DoubleAnimation(0.86, 1.0, TimeSpan.FromMilliseconds(110))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        PopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        PopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    public void HideOverlay()
    {
        _currentPage = PiePage.Main;
        PieCanvas.IsInSubPage = false;
        OutputDevicePopupBorder.Visibility = Visibility.Collapsed;
        PieCanvas.IsTriggerHeld = false;
        PieCanvas.CancelDragAndReleaseCapture();
        Mouse.Capture(null);
        if (IsVisible)
        {
            Hide();
        }
    }

    public void OnGlobalMouseDown(int screenX, int screenY)
    {
        if (!IsVisible) return;

        var (scaleX, scaleY) = GetScreenDpiScale(new POINT { X = screenX, Y = screenY });

        double dipX = screenX / scaleX;
        double dipY = screenY / scaleY;

        double relX = dipX - Left;
        double relY = dipY - Top;

        // If click is outside the PieOverlayWindow rectangle, close the overlay
        if (relX < 0 || relX > Width || relY < 0 || relY > Height)
        {
            HideOverlay();
            return;
        }

        double s = Math.Clamp(_settingsService.Current.MenuScale, 60, 160) / 100.0;
        double localX = relX / s;
        double localY = relY / s;

        // Top output device switcher bar (Y in [4..52], X in [40..560])
        if (localY >= 4 && localY <= 52 && localX >= 40 && localX <= 560)
        {
            return;
        }

        // If output device popup is visible, allow clicks inside its bounds (X in [105..495], Y in [48..400])
        if (OutputDevicePopupBorder.Visibility == Visibility.Visible)
        {
            if (localX >= 105 && localX <= 495 && localY >= 48 && localY <= 400)
            {
                return;
            }
            // Clicked outside popup: collapse popup first
            OutputDevicePopupBorder.Visibility = Visibility.Collapsed;
            UpdateOutputDeviceChevron();
            return;
        }

        // Otherwise check radial distance from the pie center (300, 320)
        double dx = localX - 300.0;
        double dy = localY - 320.0;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double maxRadius = PieCanvas.SliderMode == PieSliderMode.OuterArcExpand ? 258.0 : 246.0;
        if (dist > maxRadius)
        {
            HideOverlay();
        }
    }

    private void RefreshOutputDeviceBar()
    {
        _cachedDevices = _audioService.GetOutputDevices();
        var active = _cachedDevices.FirstOrDefault(d => d.IsDefault) ?? _cachedDevices.FirstOrDefault();

        if (active != null)
        {
            TxtOutputDeviceIcon.Text = active.IsHeadphones ? "🎧" : "🔊";
            TxtOutputDeviceName.Text = active.Name;
        }
        else
        {
            TxtOutputDeviceIcon.Text = "🔊";
            TxtOutputDeviceName.Text = "既定の出力デバイス";
        }

        UpdateOutputDeviceChevron();
    }

    private void UpdateOutputDeviceChevron()
    {
        TxtOutputDeviceChevron.Text = OutputDevicePopupBorder.Visibility == Visibility.Visible ? "▴" : "▾";
    }

    private void RebuildOutputDevicePopupList()
    {
        var palette = ThemeService.GetPalette(_settingsService.Current.ThemeMode, _settingsService.Current.ColorPreset);
        OutputDeviceListPanel.Children.Clear();

        _cachedDevices = _audioService.GetOutputDevices();
        if (_cachedDevices.Count == 0)
        {
            OutputDeviceListPanel.Children.Add(new TextBlock
            {
                Text = "利用可能な出力デバイスが見つかりません",
                Foreground = new SolidColorBrush(palette.TextSecondary),
                FontSize = 12,
                Margin = new Thickness(8, 6, 8, 6)
            });
            return;
        }

        foreach (var dev in _cachedDevices)
        {
            bool isSelected = dev.IsDefault;

            var rowBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 9, 12, 9),
                Margin = new Thickness(2, 2, 2, 2),
                Background = new SolidColorBrush(isSelected ? palette.ButtonBg : Colors.Transparent),
                BorderBrush = new SolidColorBrush(isSelected ? palette.ButtonBorder : Colors.Transparent),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Windows 11 style left vertical indicator bar for the active output device
            var indicator = new Border
            {
                Width = 3.5,
                Height = 18,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(isSelected ? palette.GaugeLeadingEdge : Colors.Transparent)
            };
            Grid.SetColumn(indicator, 0);
            rowGrid.Children.Add(indicator);

            var iconTxt = new TextBlock
            {
                Text = dev.IsHeadphones ? "🎧" : "🔊",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(isSelected ? palette.TextPrimary : palette.TextSecondary)
            };
            Grid.SetColumn(iconTxt, 1);
            rowGrid.Children.Add(iconTxt);

            var nameTxt = new TextBlock
            {
                Text = dev.Name,
                FontSize = 12.5,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Medium,
                Foreground = new SolidColorBrush(isSelected ? palette.TextPrimary : palette.ButtonText),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = dev.Name
            };
            Grid.SetColumn(nameTxt, 2);
            rowGrid.Children.Add(nameTxt);

            rowBorder.Child = rowGrid;

            rowBorder.MouseEnter += (_, _) =>
            {
                if (!isSelected)
                {
                    rowBorder.Background = new SolidColorBrush(palette.ButtonBg);
                    rowBorder.BorderBrush = new SolidColorBrush(palette.CardBorder);
                }
            };
            rowBorder.MouseLeave += (_, _) =>
            {
                if (!isSelected)
                {
                    rowBorder.Background = new SolidColorBrush(Colors.Transparent);
                    rowBorder.BorderBrush = new SolidColorBrush(Colors.Transparent);
                }
            };

            string targetDeviceId = dev.Id;
            rowBorder.MouseLeftButtonDown += (_, args) =>
            {
                args.Handled = true;
                SelectOutputDevice(targetDeviceId);
            };

            OutputDeviceListPanel.Children.Add(rowBorder);
        }
    }

    private void SelectOutputDevice(string deviceId)
    {
        if (_audioService.SetDefaultOutputDevice(deviceId))
        {
            OutputDevicePopupBorder.Visibility = Visibility.Collapsed;
            RefreshOutputDeviceBar();
            RefreshPieItems();
        }
    }

    private void BtnOutputDevice_Click(object sender, RoutedEventArgs e)
    {
        if (OutputDevicePopupBorder.Visibility == Visibility.Visible)
        {
            OutputDevicePopupBorder.Visibility = Visibility.Collapsed;
        }
        else
        {
            RebuildOutputDevicePopupList();
            OutputDevicePopupBorder.Visibility = Visibility.Visible;
        }
        UpdateOutputDeviceChevron();
        UpdateChromeVisuals(ThemeService.GetPalette(_settingsService.Current.ThemeMode, _settingsService.Current.ColorPreset));
    }

    private void TopDeviceBarBorder_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _cachedDevices = _audioService.GetOutputDevices();
        if (_cachedDevices.Count <= 1) return;

        int curIdx = _cachedDevices.FindIndex(d => d.IsDefault);
        if (curIdx < 0) curIdx = 0;

        int nextIdx = e.Delta > 0
            ? (curIdx - 1 + _cachedDevices.Count) % _cachedDevices.Count
            : (curIdx + 1) % _cachedDevices.Count;

        SelectOutputDevice(_cachedDevices[nextIdx].Id);
        if (OutputDevicePopupBorder.Visibility == Visibility.Visible)
        {
            RebuildOutputDevicePopupList();
        }
        e.Handled = true;
    }

    private void RefreshPieItems()
    {
        var s = _settingsService.Current;
        var items = new List<PieItem>();

        if (_currentPage == PiePage.Apps)
        {
            // Sub-page: Show all per-app audio sessions
            var appItems = _audioService.GetAppSessionItems(12);
            items.AddRange(appItems);

            if (items.Count == 0)
            {
                // If all apps closed, return to main
                _currentPage = PiePage.Main;
                PieCanvas.IsInSubPage = false;
                RefreshPieItems();
                return;
            }
        }
        else
        {
            // Main page: 4-5 spacious, crystal-clear slices
            // 1. Master Volume
            if (s.IncludeMasterVolume)
            {
                var (vol, muted, _) = _audioService.GetMasterOutputState();
                items.Add(new PieItem
                {
                    Id = "master_vol",
                    Title = "マスター音量",
                    Subtitle = TxtOutputDeviceName.Text,
                    Category = PieItemCategory.MasterVolume,
                    Value = vol,
                    IsMuted = muted,
                    CanMute = true,
                    AccentColor = System.Windows.Media.Color.FromRgb(56, 189, 248),
                    OnValueChanged = v => _audioService.SetMasterOutputVolume(v),
                    OnToggleMute = () => _audioService.ToggleMasterOutputMute()
                });
            }

            // 2. Screen Brightness
            if (s.IncludeBrightness)
            {
                _brightnessService.RefreshBrightnessAsync();
                double br = _brightnessService.GetBrightness();
                items.Add(new PieItem
                {
                    Id = "screen_brightness",
                    Title = "画面の明るさ",
                    Subtitle = _brightnessService.BackendDescription,
                    Category = PieItemCategory.Brightness,
                    Value = br,
                    IsMuted = false,
                    CanMute = false,
                    AccentColor = System.Windows.Media.Color.FromRgb(251, 191, 36),
                    OnValueChanged = v => _brightnessService.SetBrightness(v)
                });
            }

            // 3. Microphone Input Volume
            if (s.IncludeMicVolume)
            {
                var (micVol, micMuted, micAvail) = _audioService.GetMicInputState();
                if (micAvail)
                {
                    items.Add(new PieItem
                    {
                        Id = "mic_vol",
                        Title = "マイク音量",
                        Subtitle = "マイク入力",
                        Category = PieItemCategory.MicVolume,
                        Value = micVol,
                        IsMuted = micMuted,
                        CanMute = true,
                        AccentColor = System.Windows.Media.Color.FromRgb(52, 211, 153),
                        OnValueChanged = v => _audioService.SetMicInputVolume(v),
                        OnToggleMute = () => _audioService.ToggleMicInputMute()
                    });
                }
            }

            // 4. Per-App Audio Sessions on Main Page
            if (s.IncludeAppSessions)
            {
                var allApps = _audioService.GetAppSessionItems(12);

                if (allApps.Count == 1)
                {
                    // Single app running on system: show it directly on main page
                    items.Add(allApps[0]);
                }
                else if (allApps.Count > 1)
                {
                    // If apps are actively playing sound, promote the top active app(s) (up to 2) to main page
                    var activeApps = allApps.Where(a => a.IsActivePlaying).Take(2).ToList();
                    items.AddRange(activeApps);

                    // Add drill-down navigation slice to open the full app mixer
                    items.Add(new PieItem
                    {
                        Id = "nav_apps",
                        Title = "アプリ一覧",
                        Subtitle = $"{allApps.Count}個のアプリ ▶",
                        Category = PieItemCategory.Navigation,
                        Value = 0,
                        CustomDisplayValue = "一覧 ▶",
                        CanMute = false,
                        AccentColor = System.Windows.Media.Color.FromRgb(168, 85, 247),
                        OnInvoked = () => SwitchToPage(PiePage.Apps)
                    });
                }
            }
        }

        // Ensure at least Master Volume if everything else was empty
        if (items.Count == 0)
        {
            var (vol, muted, _) = _audioService.GetMasterOutputState();
            items.Add(new PieItem
            {
                Id = "master_vol",
                Title = "マスター音量",
                Subtitle = TxtOutputDeviceName.Text,
                Category = PieItemCategory.MasterVolume,
                Value = vol,
                IsMuted = muted,
                CanMute = true,
                AccentColor = System.Windows.Media.Color.FromRgb(56, 189, 248),
                OnValueChanged = v => _audioService.SetMasterOutputVolume(v),
                OnToggleMute = () => _audioService.ToggleMasterOutputMute()
            });
        }

        PieCanvas.SetItems(items);
    }

    private void SwitchToPage(PiePage page)
    {
        if (_currentPage == page) return;

        _currentPage = page;
        PieCanvas.IsInSubPage = (page == PiePage.Apps);
        RefreshPieItems();
        PieCanvas.ResetInteractionState();

        // Snappy page transition pop animation
        var anim = new DoubleAnimation(0.94, 1.0, TimeSpan.FromMilliseconds(95))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        PopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        PopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    private void PositionWindowAtCursor()
    {
        GetCursorPos(out POINT pt);
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(pt.X, pt.Y));
        var workArea = screen.WorkingArea;

        var (scaleX, scaleY) = GetScreenDpiScale(pt);

        double cursorDipX = pt.X / scaleX;
        double cursorDipY = pt.Y / scaleY;

        double workLeft = workArea.Left / scaleX;
        double workTop = workArea.Top / scaleY;
        double workRight = workArea.Right / scaleX;
        double workBottom = workArea.Bottom / scaleY;

        double s = Math.Clamp(_settingsService.Current.MenuScale, 60, 160) / 100.0;

        Width = Math.Round(600.0 * s);
        Height = Math.Round(610.0 * s);

        // Center of PieCanvas is at (300.0 * s, 320.0 * s)
        double targetLeft = cursorDipX - (300.0 * s);
        double targetTop = cursorDipY - (320.0 * s);

        Left = Math.Clamp(targetLeft, workLeft + 4, Math.Max(workLeft + 4, workRight - Width - 4));
        Top = Math.Clamp(targetTop, workTop + 4, Math.Max(workTop + 4, workBottom - Height - 4));

        if (!IsVisible && _settingsService.Current.SurfaceTexture == UiSurfaceTexture.Acrylic)
        {
            int capLeftPx = (int)Math.Round((Left + 30.0 * s) * scaleX);
            int capTopPx = (int)Math.Round((Top + 50.0 * s) * scaleY);
            int capWidthPx = (int)Math.Round(540.0 * s * scaleX);
            int capHeightPx = (int)Math.Round(540.0 * s * scaleY);
            var palette = ThemeService.GetPalette(_settingsService.Current.ThemeMode, _settingsService.Current.ColorPreset);
            PieCanvas.BackdropAcrylicBrush = ThemeService.CreateDesktopBlurredAcrylicBrush(capLeftPx, capTopPx, capWidthPx, capHeightPx, palette);
        }
        else if (_settingsService.Current.SurfaceTexture != UiSurfaceTexture.Acrylic)
        {
            PieCanvas.BackdropAcrylicBrush = null;
        }
    }

    private void UpdateChromeVisuals(ThemePalette palette)
    {
        // Top Output Device Switcher Bar
        TopDeviceBarBorder.Background = new SolidColorBrush(palette.SliceBgSelected);
        TopDeviceBarBorder.BorderBrush = new SolidColorBrush(palette.CardBorder);
        TxtOutputDeviceIcon.Foreground = new SolidColorBrush(palette.TextPrimary);
        TxtOutputDeviceLabel.Foreground = new SolidColorBrush(palette.TextSecondary);
        TxtOutputDeviceName.Foreground = new SolidColorBrush(palette.TextPrimary);
        TxtOutputDeviceChevron.Foreground = new SolidColorBrush(palette.TextSecondary);

        // Output Device Popup Card
        OutputDevicePopupBorder.Background = new SolidColorBrush(palette.WindowBg);
        OutputDevicePopupBorder.BorderBrush = new SolidColorBrush(palette.SliceBorderSelected);
        TxtDevicePopupTitle.Foreground = new SolidColorBrush(palette.TextPrimary);
        TxtDevicePopupHint.Foreground = new SolidColorBrush(palette.TextSecondary);

        if (OutputDevicePopupBorder.Visibility == Visibility.Visible)
        {
            RebuildOutputDevicePopupList();
        }
    }

    private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, RootGrid))
        {
            if (OutputDevicePopupBorder.Visibility == Visibility.Visible)
            {
                OutputDevicePopupBorder.Visibility = Visibility.Collapsed;
                UpdateOutputDeviceChevron();
                return;
            }
            HideOverlay();
        }
    }

    public async void OnRightChordContextMenuSuppressRequested()
    {
        _suppressDeactivateAndEscapeUntilUtc = DateTime.UtcNow.AddMilliseconds(320);

        DismissContextMenuAndReassertFocus();
        await Task.Delay(25);
        DismissContextMenuAndReassertFocus();
        await Task.Delay(55);
        DismissContextMenuAndReassertFocus();
    }

    private void DismissContextMenuAndReassertFocus()
    {
        IntPtr menuHwnd = FindWindow("#32768", null);
        if (menuHwnd != IntPtr.Zero)
        {
            PostMessage(menuHwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        keybd_event(VK_ESCAPE, 0, 0, UIntPtr.Zero);
        keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

        if (IsVisible)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                SetForegroundWindow(hwnd);
            }
            Activate();
            PieCanvas.Focus();
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (DateTime.UtcNow < _suppressDeactivateAndEscapeUntilUtc)
            {
                e.Handled = true;
                return;
            }

            if (OutputDevicePopupBorder.Visibility == Visibility.Visible)
            {
                OutputDevicePopupBorder.Visibility = Visibility.Collapsed;
                UpdateOutputDeviceChevron();
                e.Handled = true;
                return;
            }

            if (_currentPage == PiePage.Apps)
            {
                SwitchToPage(PiePage.Main);
                e.Handled = true;
                return;
            }

            HideOverlay();
            e.Handled = true;
        }
        else if (e.Key == Key.D || e.Key == Key.O)
        {
            BtnOutputDevice_Click(sender, e);
            e.Handled = true;
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (DateTime.UtcNow < _suppressDeactivateAndEscapeUntilUtc)
        {
            return;
        }
        HideOverlay();
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WM_CLOSE = 0x0010;
    private const byte VK_ESCAPE = 0x1B;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private (double scaleX, double scaleY) GetScreenDpiScale(POINT pt)
    {
        try
        {
            var hMon = MonitorFromPoint(pt, 2 /* MONITOR_DEFAULTTONEAREST */);
            if (hMon != IntPtr.Zero && GetDpiForMonitor(hMon, 0 /* MDT_EFFECTIVE_DPI */, out uint dpiX, out uint dpiY) == 0 && dpiX > 0 && dpiY > 0)
            {
                return (dpiX / 96.0, dpiY / 96.0);
            }
        }
        catch
        {
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        return (dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0, dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
