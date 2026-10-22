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
    private List<AudioDeviceInfo> _cachedOutputDevices = new();
    private List<AudioDeviceInfo> _cachedInputDevices = new();
    private AudioDeviceType _activeDeviceTab = AudioDeviceType.Output;
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
        LocalizationService.LanguageChanged += () =>
        {
            Dispatcher.Invoke(() =>
            {
                RefreshDeviceBars();
                RefreshPieItems();
                PieCanvas.InvalidateVisual();
            });
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
        DevicePopupBorder.Visibility = Visibility.Collapsed;
        RefreshDeviceBars();
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
        DevicePopupBorder.Visibility = Visibility.Collapsed;
        UpdateDeviceChevrons();
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

        // Top device switcher bars (Y in [4..52], X in [30..570])
        if (localY >= 4 && localY <= 52 && localX >= 30 && localX <= 570)
        {
            return;
        }

        // If audio device popup is visible, allow clicks inside its bounds (X in [90..510], Y in [44..420])
        if (DevicePopupBorder.Visibility == Visibility.Visible)
        {
            if (localX >= 90 && localX <= 510 && localY >= 44 && localY <= 420)
            {
                return;
            }
            // Clicked outside popup: collapse popup first
            DevicePopupBorder.Visibility = Visibility.Collapsed;
            UpdateDeviceChevrons();
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

    private void RefreshDeviceBars()
    {
        _cachedOutputDevices = _audioService.GetOutputDevices();
        var activeOut = _cachedOutputDevices.FirstOrDefault(d => d.IsDefault) ?? _cachedOutputDevices.FirstOrDefault();
        if (activeOut != null)
        {
            TxtOutputDeviceIcon.Text = activeOut.IsHeadphones ? "🎧" : "🔊";
            TxtOutputDeviceName.Text = activeOut.Name;
        }
        else
        {
            TxtOutputDeviceIcon.Text = "🔊";
            TxtOutputDeviceName.Text = LocalizationService.Get("Loc_PreviewItemSpeakerOutput");
        }

        _cachedInputDevices = _audioService.GetInputDevices();
        var activeIn = _cachedInputDevices.FirstOrDefault(d => d.IsDefault) ?? _cachedInputDevices.FirstOrDefault();
        if (activeIn != null)
        {
            TxtInputDeviceIcon.Text = "🎤";
            TxtInputDeviceName.Text = activeIn.Name;
        }
        else
        {
            TxtInputDeviceIcon.Text = "🎤";
            TxtInputDeviceName.Text = LocalizationService.Get("Loc_PieMicInput");
        }

        UpdateDeviceChevrons();
    }

    private void UpdateDeviceChevrons()
    {
        if (DevicePopupBorder.Visibility == Visibility.Visible)
        {
            TxtOutputDeviceChevron.Text = _activeDeviceTab == AudioDeviceType.Output ? "▴" : "▾";
            TxtInputDeviceChevron.Text = _activeDeviceTab == AudioDeviceType.Input ? "▴" : "▾";
        }
        else
        {
            TxtOutputDeviceChevron.Text = "▾";
            TxtInputDeviceChevron.Text = "▾";
        }
    }

    private void RebuildDevicePopupList()
    {
        var palette = ThemeService.GetPalette(_settingsService.Current.ThemeMode, _settingsService.Current.ColorPreset);
        DeviceListPanel.Children.Clear();

        bool isOutput = _activeDeviceTab == AudioDeviceType.Output;
        BtnPopupTabOutput.Background = new SolidColorBrush(isOutput ? palette.ButtonActiveBg : Colors.Transparent);
        BtnPopupTabOutput.BorderBrush = new SolidColorBrush(isOutput ? palette.GaugeLeadingEdge : palette.CardBorder);
        TxtPopupTabOutputLabel.Foreground = new SolidColorBrush(isOutput ? palette.ButtonActiveText : palette.TextSecondary);

        BtnPopupTabInput.Background = new SolidColorBrush(!isOutput ? palette.ButtonActiveBg : Colors.Transparent);
        BtnPopupTabInput.BorderBrush = new SolidColorBrush(!isOutput ? palette.GaugeLeadingEdge : palette.CardBorder);
        TxtPopupTabInputLabel.Foreground = new SolidColorBrush(!isOutput ? palette.ButtonActiveText : palette.TextSecondary);

        var devices = isOutput ? _audioService.GetOutputDevices() : _audioService.GetInputDevices();
        if (isOutput) _cachedOutputDevices = devices;
        else _cachedInputDevices = devices;

        if (devices.Count == 0)
        {
            DeviceListPanel.Children.Add(new TextBlock
            {
                Text = LocalizationService.Get(isOutput ? "Loc_PieNoOutputDevices" : "Loc_PieNoInputDevices"),
                Foreground = new SolidColorBrush(palette.TextSecondary),
                FontSize = 12,
                Margin = new Thickness(8, 8, 8, 8)
            });
            return;
        }

        foreach (var dev in devices)
        {
            bool isSelected = dev.IsDefault;

            var rowBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 12, 8),
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
                Text = dev.Icon,
                FontSize = 13.5,
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

            var targetDev = dev;
            rowBorder.MouseLeftButtonDown += (_, args) =>
            {
                args.Handled = true;
                SelectDevice(targetDev);
            };

            DeviceListPanel.Children.Add(rowBorder);
        }
    }

    private void SelectDevice(AudioDeviceInfo dev)
    {
        bool ok = dev.DeviceType == AudioDeviceType.Input
            ? _audioService.SetDefaultInputDevice(dev.Id)
            : _audioService.SetDefaultOutputDevice(dev.Id);

        if (ok)
        {
            DevicePopupBorder.Visibility = Visibility.Collapsed;
            RefreshDeviceBars();
            RefreshPieItems();
        }
    }

    private void BtnOutputDevice_Click(object sender, RoutedEventArgs e)
    {
        if (DevicePopupBorder.Visibility == Visibility.Visible && _activeDeviceTab == AudioDeviceType.Output)
        {
            DevicePopupBorder.Visibility = Visibility.Collapsed;
        }
        else
        {
            _activeDeviceTab = AudioDeviceType.Output;
            RebuildDevicePopupList();
            DevicePopupBorder.Visibility = Visibility.Visible;
        }
        UpdateDeviceChevrons();
        UpdateChromeVisuals(ThemeService.GetPalette(_settingsService.Current.ThemeMode, _settingsService.Current.ColorPreset));
    }

    private void BtnInputDevice_Click(object sender, RoutedEventArgs e)
    {
        if (DevicePopupBorder.Visibility == Visibility.Visible && _activeDeviceTab == AudioDeviceType.Input)
        {
            DevicePopupBorder.Visibility = Visibility.Collapsed;
        }
        else
        {
            _activeDeviceTab = AudioDeviceType.Input;
            RebuildDevicePopupList();
            DevicePopupBorder.Visibility = Visibility.Visible;
        }
        UpdateDeviceChevrons();
        UpdateChromeVisuals(ThemeService.GetPalette(_settingsService.Current.ThemeMode, _settingsService.Current.ColorPreset));
    }

    private void BtnPopupTabOutput_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDeviceTab == AudioDeviceType.Output) return;
        _activeDeviceTab = AudioDeviceType.Output;
        RebuildDevicePopupList();
        UpdateDeviceChevrons();
    }

    private void BtnPopupTabInput_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDeviceTab == AudioDeviceType.Input) return;
        _activeDeviceTab = AudioDeviceType.Input;
        RebuildDevicePopupList();
        UpdateDeviceChevrons();
    }

    private void TopOutputDeviceBarBorder_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _cachedOutputDevices = _audioService.GetOutputDevices();
        if (_cachedOutputDevices.Count <= 1) return;

        int curIdx = _cachedOutputDevices.FindIndex(d => d.IsDefault);
        if (curIdx < 0) curIdx = 0;

        int nextIdx = e.Delta > 0
            ? (curIdx - 1 + _cachedOutputDevices.Count) % _cachedOutputDevices.Count
            : (curIdx + 1) % _cachedOutputDevices.Count;

        SelectDevice(_cachedOutputDevices[nextIdx]);
        if (DevicePopupBorder.Visibility == Visibility.Visible && _activeDeviceTab == AudioDeviceType.Output)
        {
            RebuildDevicePopupList();
        }
        e.Handled = true;
    }

    private void TopInputDeviceBarBorder_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _cachedInputDevices = _audioService.GetInputDevices();
        if (_cachedInputDevices.Count <= 1) return;

        int curIdx = _cachedInputDevices.FindIndex(d => d.IsDefault);
        if (curIdx < 0) curIdx = 0;

        int nextIdx = e.Delta > 0
            ? (curIdx - 1 + _cachedInputDevices.Count) % _cachedInputDevices.Count
            : (curIdx + 1) % _cachedInputDevices.Count;

        SelectDevice(_cachedInputDevices[nextIdx]);
        if (DevicePopupBorder.Visibility == Visibility.Visible && _activeDeviceTab == AudioDeviceType.Input)
        {
            RebuildDevicePopupList();
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
                    Title = LocalizationService.Get("Loc_PieMasterVolume"),
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
                    Title = LocalizationService.Get("Loc_PieBrightness"),
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
                        Title = LocalizationService.Get("Loc_PieMicVolume"),
                        Subtitle = TxtInputDeviceName.Text,
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
                        Title = LocalizationService.Get("Loc_PieAppMixer"),
                        Subtitle = $"{allApps.Count} {LocalizationService.Get("Loc_PieAppsCountSuffix")}",
                        Category = PieItemCategory.Navigation,
                        Value = 0,
                        CustomDisplayValue = LocalizationService.Get("Loc_PieAppsListBadge"),
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
                Title = LocalizationService.Get("Loc_PieMasterVolume"),
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
        TopOutputDeviceBarBorder.Background = new SolidColorBrush(palette.SliceBgSelected);
        TopOutputDeviceBarBorder.BorderBrush = new SolidColorBrush(palette.CardBorder);
        TxtOutputDeviceIcon.Foreground = new SolidColorBrush(palette.TextPrimary);
        TxtOutputDeviceLabel.Foreground = new SolidColorBrush(palette.TextSecondary);
        TxtOutputDeviceName.Foreground = new SolidColorBrush(palette.TextPrimary);
        TxtOutputDeviceChevron.Foreground = new SolidColorBrush(palette.TextSecondary);

        // Top Input Device Switcher Bar
        TopInputDeviceBarBorder.Background = new SolidColorBrush(palette.SliceBgSelected);
        TopInputDeviceBarBorder.BorderBrush = new SolidColorBrush(palette.CardBorder);
        TxtInputDeviceIcon.Foreground = new SolidColorBrush(palette.TextPrimary);
        TxtInputDeviceLabel.Foreground = new SolidColorBrush(palette.TextSecondary);
        TxtInputDeviceName.Foreground = new SolidColorBrush(palette.TextPrimary);
        TxtInputDeviceChevron.Foreground = new SolidColorBrush(palette.TextSecondary);

        // Device Popup Card
        DevicePopupBorder.Background = new SolidColorBrush(palette.WindowBg);
        DevicePopupBorder.BorderBrush = new SolidColorBrush(palette.SliceBorderSelected);
        TxtDevicePopupHint.Foreground = new SolidColorBrush(palette.TextSecondary);

        if (DevicePopupBorder.Visibility == Visibility.Visible)
        {
            RebuildDevicePopupList();
        }
    }

    private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, RootGrid))
        {
            if (DevicePopupBorder.Visibility == Visibility.Visible)
            {
                DevicePopupBorder.Visibility = Visibility.Collapsed;
                UpdateDeviceChevrons();
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

            if (DevicePopupBorder.Visibility == Visibility.Visible)
            {
                DevicePopupBorder.Visibility = Visibility.Collapsed;
                UpdateDeviceChevrons();
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
        else if (e.Key == Key.O)
        {
            BtnOutputDevice_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.I)
        {
            BtnInputDevice_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.D)
        {
            if (DevicePopupBorder.Visibility == Visibility.Visible)
            {
                DevicePopupBorder.Visibility = Visibility.Collapsed;
                UpdateDeviceChevrons();
            }
            else
            {
                BtnOutputDevice_Click(sender, e);
            }
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
