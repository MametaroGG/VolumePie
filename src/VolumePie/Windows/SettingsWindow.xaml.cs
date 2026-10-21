using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using VolumePie.Models;
using VolumePie.Services;
using Color = System.Windows.Media.Color;
using ComboBox = System.Windows.Controls.ComboBox;

namespace VolumePie.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly GlobalInputHookService _hookService;
    private readonly Action _testPieAction;

    private readonly AppSettings _initialBackup;
    private readonly bool _initialRunAtStartup;
    private AppSettings _workingCopy;
    private bool _isRecordingHotkey;
    private bool _isInitializing = true;
    private bool _previewLightBackdrop;
    private int _activeTabIndex;

    public SettingsWindow(
        SettingsService settingsService,
        GlobalInputHookService hookService,
        Action testPieAction)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _hookService = hookService;
        _testPieAction = testPieAction;
        _initialBackup = _settingsService.Current.Clone();
        _initialRunAtStartup = _settingsService.IsRunAtStartupEnabled();
        _workingCopy = _settingsService.Current.Clone();

        LoadFromSettings(_workingCopy);
        _isInitializing = false;

        RebuildPreviewPieItems();
        ApplyThemeAndRefreshPreview();
        SwitchTab(0);

        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        LocalizationService.LanguageChanged += OnLanguageChanged;

        SourceInitialized += (_, _) =>
        {
            var p = ThemeService.GetPalette(_workingCopy.ThemeMode, _workingCopy.ColorPreset);
            UpdateTitleBarTheme(p);
        };
    }

    private void OnLanguageChanged()
    {
        UpdatePreviewStageBackdrop();
        RebuildPreviewPieItems();
        ApplyThemeAndRefreshPreview();
        if (!_isRecordingHotkey)
        {
            BtnRecordHotkey.Content = _workingCopy.GetHotkeyDisplayText();
        }
    }

    private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (IsLoaded)
            {
                ApplyThemeAndRefreshPreview();
            }
        });
    }

    private void SwitchTab(int tabIndex)
    {
        _activeTabIndex = Math.Clamp(tabIndex, 0, 2);
        TabPanelAppearance.Visibility = _activeTabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        TabPanelSliderMode.Visibility = _activeTabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        TabPanelTriggers.Visibility = _activeTabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;

        var p = ThemeService.GetPalette(_workingCopy.ThemeMode, _workingCopy.ColorPreset);
        UpdateTabButtonStyles(p);
    }

    private void UpdateTabButtonStyles(ThemePalette p)
    {
        void StyleTabBtn(System.Windows.Controls.Button btn, bool active)
        {
            btn.Background = new SolidColorBrush(active ? p.ButtonActiveBg : Colors.Transparent);
            btn.BorderBrush = new SolidColorBrush(active ? p.ButtonActiveBorder : Colors.Transparent);
            btn.Foreground = new SolidColorBrush(active ? p.ButtonActiveText : p.TextSecondary);
        }

        StyleTabBtn(BtnTabAppearance, _activeTabIndex == 0);
        StyleTabBtn(BtnTabSliderMode, _activeTabIndex == 1);
        StyleTabBtn(BtnTabTriggers, _activeTabIndex == 2);
    }

    private void BtnTabAppearance_Click(object sender, RoutedEventArgs e) => SwitchTab(0);
    private void BtnTabSliderMode_Click(object sender, RoutedEventArgs e) => SwitchTab(1);
    private void BtnTabTriggers_Click(object sender, RoutedEventArgs e) => SwitchTab(2);

    private void BtnTogglePreviewBackdrop_Click(object sender, RoutedEventArgs e)
    {
        _previewLightBackdrop = !_previewLightBackdrop;
        UpdatePreviewStageBackdrop();
    }

    private void UpdatePreviewStageBackdrop()
    {
        if (_previewLightBackdrop)
        {
            PreviewStageBorder.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            BtnTogglePreviewBackdrop.Content = LocalizationService.Get("Loc_PreviewBackdropLight");
        }
        else
        {
            PreviewStageBorder.Background = new SolidColorBrush(Color.FromRgb(18, 19, 24));
            BtnTogglePreviewBackdrop.Content = LocalizationService.Get("Loc_PreviewBackdropDark");
        }
    }

    private void RebuildPreviewPieItems()
    {
        var items = new List<PieItem>();

        if (_workingCopy.IncludeMasterVolume)
        {
            var masterItem = new PieItem
            {
                Id = "preview_master",
                Title = LocalizationService.Get("Loc_PreviewItemMasterVolume"),
                Subtitle = LocalizationService.Get("Loc_PreviewItemSpeakerOutput"),
                Category = PieItemCategory.MasterVolume,
                Value = 74,
                IsMuted = false,
                CanMute = true
            };
            masterItem.OnToggleMute = () => masterItem.IsMuted = !masterItem.IsMuted;
            items.Add(masterItem);
        }

        if (_workingCopy.IncludeBrightness)
        {
            items.Add(new PieItem
            {
                Id = "preview_brightness",
                Title = LocalizationService.Get("Loc_PreviewItemBrightness"),
                Subtitle = LocalizationService.Get("Loc_PreviewItemDisplayBrightness"),
                Category = PieItemCategory.Brightness,
                Value = 85,
                IsMuted = false,
                CanMute = false
            });
        }

        if (_workingCopy.IncludeMicVolume)
        {
            var micItem = new PieItem
            {
                Id = "preview_mic",
                Title = LocalizationService.Get("Loc_PreviewItemMicVolume"),
                Subtitle = LocalizationService.Get("Loc_PreviewItemMicInput"),
                Category = PieItemCategory.MicVolume,
                Value = 62,
                IsMuted = false,
                CanMute = true
            };
            micItem.OnToggleMute = () => micItem.IsMuted = !micItem.IsMuted;
            items.Add(micItem);
        }

        if (_workingCopy.IncludeAppSessions)
        {
            var mediaItem = new PieItem
            {
                Id = "preview_media",
                Title = "Media Player",
                Subtitle = LocalizationService.Get("Loc_PreviewItemAppVolume"),
                Category = PieItemCategory.AppAudio,
                Value = 52,
                IsMuted = false,
                CanMute = true
            };
            mediaItem.OnToggleMute = () => mediaItem.IsMuted = !mediaItem.IsMuted;
            items.Add(mediaItem);

            var browserItem = new PieItem
            {
                Id = "preview_browser",
                Title = LocalizationService.Get("Loc_PreviewItemBrowser"),
                Subtitle = LocalizationService.Get("Loc_PreviewItemAppVolume"),
                Category = PieItemCategory.AppAudio,
                Value = 68,
                IsMuted = false,
                CanMute = true
            };
            browserItem.OnToggleMute = () => browserItem.IsMuted = !browserItem.IsMuted;
            items.Add(browserItem);
        }

        if (items.Count == 0)
        {
            items.Add(new PieItem
            {
                Id = "preview_master",
                Title = LocalizationService.Get("Loc_PreviewItemMasterVolume"),
                Category = PieItemCategory.MasterVolume,
                Value = 74,
                CanMute = true
            });
        }

        PreviewPieCanvas.SetItems(items);
        PreviewPieCanvas.PreviewFallbackIndex = 0;
    }

    private void ApplyThemeAndRefreshPreview()
    {
        var p = ThemeService.GetPalette(_workingCopy.ThemeMode, _workingCopy.ColorPreset);
        Resources["WindowBgBrush"] = new SolidColorBrush(p.WindowBg);
        Resources["CardBgBrush"] = new SolidColorBrush(p.CardBg);
        Resources["CardBorderBrush"] = new SolidColorBrush(p.CardBorder);
        Resources["TextPrimaryBrush"] = new SolidColorBrush(p.TextPrimary);
        Resources["TextSecondaryBrush"] = new SolidColorBrush(p.TextSecondary);
        Resources["ButtonBgBrush"] = new SolidColorBrush(p.ButtonBg);
        Resources["ButtonBorderBrush"] = new SolidColorBrush(p.ButtonBorder);
        Resources["ButtonTextBrush"] = new SolidColorBrush(p.ButtonText);
        Resources["ButtonActiveBgBrush"] = new SolidColorBrush(p.ButtonActiveBg);
        Resources["ButtonActiveTextBrush"] = new SolidColorBrush(p.ButtonActiveText);

        UpdateTabButtonStyles(p);

        // Update Live Interactive PieMenuCanvas Preview
        PreviewPieCanvas.Palette = p;
        PreviewPieCanvas.SurfaceTexture = _workingCopy.SurfaceTexture;
        PreviewPieCanvas.LightingEffect = _workingCopy.LightingEffect;
        PreviewPieCanvas.SliderMode = _workingCopy.SliderMode;
        PreviewPieCanvas.WheelStep = _workingCopy.WheelStep;
        PreviewPieCanvas.SliceLabelStyle = _workingCopy.SliceLabelStyle;
        PreviewPieCanvas.PreviewFallbackIndex = 0;

        if (PreviewViewbox != null)
        {
            double scaleRatio = Math.Clamp(_workingCopy.MenuScale, 60, 160) / 100.0;
            PreviewViewbox.Width = Math.Round(260.0 * scaleRatio);
            PreviewViewbox.Height = Math.Round(260.0 * scaleRatio);
        }

        string texShort = AppSettings.GetTextureTitle(_workingCopy.SurfaceTexture);
        string lightShort = AppSettings.GetLightingTitle(_workingCopy.LightingEffect);
        TxtCurrentStyleSummary.Text = $"{texShort} / {lightShort} / {AppSettings.GetColorPresetTitle(_workingCopy.ColorPreset)}";

        RebuildCloseUpPreviewBar(p);
        RebuildTextureCardsGallery(p);
        RebuildLightingCardsGallery(p);
        RebuildThemeCardsGallery(p);
        UpdateTitleBarTheme(p);

        if (!_isInitializing)
        {
            // Live-apply to SettingsService so opening the real Pie Menu immediately reflects the previewed customization
            _settingsService.Save(_workingCopy.Clone());
        }
    }

    private void RebuildCloseUpPreviewBar(ThemePalette p)
    {
        if (CloseUpPreviewHost == null) return;
        CloseUpPreviewHost.Children.Clear();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });

        // 1. Zoomed-in Horizontal Textured Slider Bar (like Sound Assistant "バー")
        var barContainer = new Grid
        {
            Height = 68,
            VerticalAlignment = VerticalAlignment.Center
        };

        bool isAcrylic = _workingCopy.SurfaceTexture == UiSurfaceTexture.Acrylic;

        var trackBorder = new Border
        {
            CornerRadius = new CornerRadius(18),
            Background = isAcrylic
                ? ThemeService.GetSimulatedAcrylicBackdropBrush(p)
                : new SolidColorBrush(p.TrackBackground),
            BorderBrush = isAcrylic
                ? ThemeService.GetAcrylicSpecularBorderBrush(true)
                : new SolidColorBrush(p.SliceBorderSelected),
            BorderThickness = new Thickness(1.5)
        };
        if (_workingCopy.LightingEffect != UiLightingEffect.Minimal)
        {
            var glowCol = _workingCopy.LightingEffect == UiLightingEffect.PrismAurora
                ? Color.FromRgb(167, 139, 250)
                : p.GlowColor;
            trackBorder.Effect = new DropShadowEffect
            {
                Color = glowCol,
                BlurRadius = _workingCopy.LightingEffect == UiLightingEffect.EdgeRimGlow ? 16 : 10,
                ShadowDepth = 0,
                Opacity = _workingCopy.LightingEffect == UiLightingEffect.EdgeRimGlow ? 0.75 : 0.45
            };
        }
        barContainer.Children.Add(trackBorder);

        if (isAcrylic)
        {
            barContainer.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = ThemeService.GetAcrylicGlassSheenBrush(p.IsLight, false)
            });
        }

        var trackTex = ThemeService.GetTextureOverlayBrush(_workingCopy.SurfaceTexture, p.IsLight, isStrong: false);
        if (trackTex != null)
        {
            barContainer.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = trackTex
            });
        }

        // Filled 74% portion of the zoomed-in bar
        var fillHost = new Grid
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Width = 215,
            Margin = new Thickness(4)
        };
        System.Windows.Media.Brush barFillBrush = isAcrylic
            ? ThemeService.GetAcrylicFrostedPillFillBrush(p, true)
            : (_workingCopy.LightingEffect == UiLightingEffect.Minimal
                ? new SolidColorBrush(p.GaugeFillSelectedEnd)
                : new LinearGradientBrush(p.GaugeFillSelectedStart, p.GaugeFillSelectedEnd, 0.0));
        fillHost.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = barFillBrush,
            BorderBrush = isAcrylic ? ThemeService.GetAcrylicSpecularBorderBrush(true) : null,
            BorderThickness = isAcrylic ? new Thickness(1.0) : new Thickness(0)
        });

        if (_workingCopy.LightingEffect == UiLightingEffect.PrismAurora)
        {
            var prism = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1)
            };
            prism.GradientStops.Add(new GradientStop(Color.FromArgb(135, 56, 189, 248), 0.0));
            prism.GradientStops.Add(new GradientStop(Color.FromArgb(135, 168, 85, 247), 0.36));
            prism.GradientStops.Add(new GradientStop(Color.FromArgb(135, 244, 114, 182), 0.68));
            prism.GradientStops.Add(new GradientStop(Color.FromArgb(135, 52, 211, 153), 1.0));
            fillHost.Children.Add(new Border { CornerRadius = new CornerRadius(15), Background = prism });
        }
        else if (_workingCopy.LightingEffect == UiLightingEffect.SoftGlow)
        {
            var spot = new RadialGradientBrush(Color.FromArgb(95, 255, 255, 255), Colors.Transparent);
            fillHost.Children.Add(new Border { CornerRadius = new CornerRadius(15), Background = spot });
        }

        var strongTex = ThemeService.GetTextureOverlayBrush(_workingCopy.SurfaceTexture, p.IsLight, isStrong: true);
        if (strongTex != null)
        {
            fillHost.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(15),
                Background = strongTex
            });
        }
        barContainer.Children.Add(fillHost);

        var barLabelRow = new DockPanel
        {
            Margin = new Thickness(16, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        barLabelRow.Children.Add(new TextBlock
        {
            Text = LocalizationService.Get("Loc_PreviewItemMasterVolume"),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(p.TextPrimary),
            VerticalAlignment = VerticalAlignment.Center
        });
        barLabelRow.Children.Add(new TextBlock
        {
            Text = "74%",
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(p.TextPrimary),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        });
        barContainer.Children.Add(barLabelRow);

        Grid.SetColumn(barContainer, 0);
        grid.Children.Add(barContainer);

        // 2. Zoomed-in Circular Knob (like Sound Assistant "ノブ" at 00:03 of video)
        var knobGrid = new Grid
        {
            Width = 72,
            Height = 72,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        System.Windows.Media.Brush knobFillBrush = isAcrylic
            ? ThemeService.GetSimulatedAcrylicBackdropBrush(p)
            : (_workingCopy.LightingEffect == UiLightingEffect.Minimal
                ? new SolidColorBrush(p.GaugeFillSelectedEnd)
                : new RadialGradientBrush(p.GaugeFillSelectedEnd, p.GaugeFillSelectedStart));
        var knobBase = new Border
        {
            CornerRadius = new CornerRadius(36),
            Background = knobFillBrush,
            BorderBrush = isAcrylic
                ? ThemeService.GetAcrylicSpecularBorderBrush(true)
                : new SolidColorBrush(p.SliceBorderSelected),
            BorderThickness = new Thickness(1.6)
        };
        if (_workingCopy.LightingEffect != UiLightingEffect.Minimal)
        {
            knobBase.Effect = new DropShadowEffect
            {
                Color = p.GlowColor,
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = 0.55
            };
        }
        knobGrid.Children.Add(knobBase);

        if (isAcrylic)
        {
            knobGrid.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(36),
                Background = ThemeService.GetAcrylicFrostedPillFillBrush(p, false)
            });
            knobGrid.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(36),
                Background = ThemeService.GetAcrylicGlassSheenBrush(p.IsLight, true)
            });
        }

        if (strongTex != null)
        {
            knobGrid.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(36),
                Background = strongTex
            });
        }
        knobGrid.Children.Add(new TextBlock
        {
            Text = "74",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(p.TextPrimary),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        Grid.SetColumn(knobGrid, 2);
        grid.Children.Add(knobGrid);

        CloseUpPreviewHost.Children.Add(grid);
    }

    private void RebuildTextureCardsGallery(ThemePalette currentPalette)
    {
        if (TextureCardsWrapPanel == null) return;
        TextureCardsWrapPanel.Children.Clear();

        foreach (UiSurfaceTexture tex in new[] { UiSurfaceTexture.Flat, UiSurfaceTexture.Acrylic, UiSurfaceTexture.Mesh, UiSurfaceTexture.Noise })
        {
            bool isSelected = _workingCopy.SurfaceTexture == tex;

            var card = new Border
            {
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(6, 8, 6, 7),
                Margin = new Thickness(3, 0, 3, 0),
                Background = new SolidColorBrush(isSelected ? currentPalette.ButtonActiveBg : currentPalette.ButtonBg),
                BorderBrush = new SolidColorBrush(isSelected ? currentPalette.ButtonActiveBorder : currentPalette.ButtonBorder),
                BorderThickness = new Thickness(isSelected ? 2.0 : 1.0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var stack = new StackPanel();

            var previewBox = new Grid
            {
                Height = 62,
                Margin = new Thickness(0, 0, 0, 6)
            };

            if (tex == UiSurfaceTexture.Acrylic)
            {
                // True Frosted Glass Acrylic miniature (matches reference image: frosted gradient + specular rim + milky top pill)
                previewBox.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(9),
                    Background = ThemeService.GetSimulatedAcrylicBackdropBrush(ThemePalette.Dark),
                    BorderBrush = ThemeService.GetAcrylicSpecularBorderBrush(true),
                    BorderThickness = new Thickness(1.2)
                });
                previewBox.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(9),
                    Background = ThemeService.GetAcrylicGlassSheenBrush(false, true)
                });
                previewBox.Children.Add(new Border
                {
                    Height = 16,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(8, 8, 8, 0),
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromArgb(115, 255, 255, 255))
                });
            }
            else if (tex == UiSurfaceTexture.Flat)
            {
                previewBox.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(9),
                    Background = new SolidColorBrush(Color.FromRgb(82, 84, 94)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)),
                    BorderThickness = new Thickness(1)
                });
            }
            else
            {
                var baseGrad = new RadialGradientBrush(
                    Color.FromRgb(145, 150, 162),
                    Color.FromRgb(48, 50, 58))
                {
                    Center = new System.Windows.Point(0.5, 0.5),
                    GradientOrigin = new System.Windows.Point(0.5, 0.5),
                    RadiusX = 0.65,
                    RadiusY = 0.65
                };

                previewBox.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(9),
                    Background = baseGrad,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)),
                    BorderThickness = new Thickness(1)
                });

                var texBrush = ThemeService.GetTextureOverlayBrush(tex, isLight: false, isStrong: true);
                if (texBrush != null)
                {
                    previewBox.Children.Add(new Border
                    {
                        CornerRadius = new CornerRadius(9),
                        Background = texBrush
                    });
                }
            }

            stack.Children.Add(previewBox);

            string title = AppSettings.GetTextureTitle(tex);

            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 11.5,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = new SolidColorBrush(isSelected ? currentPalette.ButtonActiveText : currentPalette.ButtonText),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            });

            card.Child = stack;
            var targetTex = tex;
            card.MouseLeftButtonDown += (_, args) =>
            {
                args.Handled = true;
                _workingCopy.SurfaceTexture = targetTex;
                ApplyThemeAndRefreshPreview();
            };

            TextureCardsWrapPanel.Children.Add(card);
        }
    }

    private void RebuildLightingCardsGallery(ThemePalette currentPalette)
    {
        if (LightingCardsWrapPanel == null) return;
        LightingCardsWrapPanel.Children.Clear();

        foreach (UiLightingEffect light in new[] { UiLightingEffect.Minimal, UiLightingEffect.SoftGlow, UiLightingEffect.EdgeRimGlow, UiLightingEffect.PrismAurora })
        {
            bool isSelected = _workingCopy.LightingEffect == light;

            var card = new Border
            {
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(6, 8, 6, 7),
                Margin = new Thickness(3, 0, 3, 0),
                Background = new SolidColorBrush(isSelected ? currentPalette.ButtonActiveBg : currentPalette.ButtonBg),
                BorderBrush = new SolidColorBrush(isSelected ? currentPalette.ButtonActiveBorder : currentPalette.ButtonBorder),
                BorderThickness = new Thickness(isSelected ? 2.0 : 1.0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var stack = new StackPanel();

            // Sound Assistant 00:01 style vertical glowing slider miniature
            var stage = new Border
            {
                Height = 84,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromRgb(22, 23, 28)),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(0, 7, 0, 7)
            };

            var vBar = new Grid
            {
                Width = 30,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            var barBorder = new Border
            {
                CornerRadius = new CornerRadius(15),
                Background = new SolidColorBrush(Color.FromRgb(42, 44, 52)),
                BorderBrush = new SolidColorBrush(
                    light == UiLightingEffect.EdgeRimGlow
                        ? Color.FromRgb(245, 245, 250)
                        : Color.FromRgb(140, 144, 158)),
                BorderThickness = new Thickness(light == UiLightingEffect.EdgeRimGlow ? 1.8 : 1.0)
            };

            if (light != UiLightingEffect.Minimal)
            {
                barBorder.Effect = new DropShadowEffect
                {
                    Color = light == UiLightingEffect.PrismAurora
                        ? Color.FromRgb(167, 139, 250)
                        : Color.FromRgb(230, 232, 240),
                    BlurRadius = light == UiLightingEffect.EdgeRimGlow ? 14 : 9,
                    ShadowDepth = 0,
                    Opacity = light == UiLightingEffect.EdgeRimGlow ? 0.85 : 0.55
                };
            }
            vBar.Children.Add(barBorder);

            var fillBorder = new Border
            {
                Height = 46,
                VerticalAlignment = VerticalAlignment.Bottom,
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(1.5)
            };

            if (light == UiLightingEffect.PrismAurora)
            {
                var prism = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint = new System.Windows.Point(1, 1)
                };
                prism.GradientStops.Add(new GradientStop(Color.FromRgb(96, 165, 250), 0.0));
                prism.GradientStops.Add(new GradientStop(Color.FromRgb(167, 139, 250), 0.45));
                prism.GradientStops.Add(new GradientStop(Color.FromRgb(244, 114, 182), 0.8));
                prism.GradientStops.Add(new GradientStop(Color.FromRgb(52, 211, 153), 1.0));
                fillBorder.Background = prism;
            }
            else if (light == UiLightingEffect.SoftGlow)
            {
                fillBorder.Background = new RadialGradientBrush(
                    Color.FromRgb(228, 230, 238),
                    Color.FromRgb(115, 118, 130));
            }
            else
            {
                fillBorder.Background = new LinearGradientBrush(
                    Color.FromRgb(185, 188, 198),
                    Color.FromRgb(105, 108, 120),
                    90.0);
            }
            vBar.Children.Add(fillBorder);

            var texBrush = ThemeService.GetTextureOverlayBrush(_workingCopy.SurfaceTexture, isLight: false, isStrong: true);
            if (texBrush != null)
            {
                vBar.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(15),
                    Background = texBrush
                });
            }

            stage.Child = vBar;
            stack.Children.Add(stage);

            string shortLabel = AppSettings.GetLightingTitle(light);

            stack.Children.Add(new TextBlock
            {
                Text = shortLabel,
                FontSize = 11.5,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = new SolidColorBrush(isSelected ? currentPalette.ButtonActiveText : currentPalette.ButtonText),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            });

            card.Child = stack;
            var targetLight = light;
            card.MouseLeftButtonDown += (_, args) =>
            {
                args.Handled = true;
                _workingCopy.LightingEffect = targetLight;
                ApplyThemeAndRefreshPreview();
            };

            LightingCardsWrapPanel.Children.Add(card);
        }
    }

    private void RebuildThemeCardsGallery(ThemePalette currentPalette)
    {
        if (ThemeCardsWrapPanel == null) return;
        ThemeCardsWrapPanel.Children.Clear();

        foreach (UiColorPreset preset in ThemeService.OrderedPresets)
        {
            bool isSelected = _workingCopy.ColorPreset == preset;
            var (surf, track, fillBot, fillTop) = ThemeService.GetPresetPreviewColors(preset);

            var outerCard = new Border
            {
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(8, 8, 8, 7),
                Margin = new Thickness(3, 3, 3, 3),
                Background = new SolidColorBrush(isSelected ? currentPalette.ButtonActiveBg : currentPalette.ButtonBg),
                BorderBrush = new SolidColorBrush(isSelected ? currentPalette.ButtonActiveBorder : currentPalette.ButtonBorder),
                BorderThickness = new Thickness(isSelected ? 2.0 : 1.0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var stack = new StackPanel();

            // Sound Assistant style 4-bar volume panel miniature
            var previewBox = new Grid
            {
                Height = 62,
                Margin = new Thickness(0, 0, 0, 6)
            };
            var bgBorder = new Border
            {
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(surf),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                BorderThickness = new Thickness(1)
            };
            previewBox.Children.Add(bgBorder);

            var texBrush = ThemeService.GetTextureOverlayBrush(_workingCopy.SurfaceTexture, isLight: preset != UiColorPreset.Monochrome, isStrong: false);
            if (texBrush != null)
            {
                previewBox.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(9),
                    Background = texBrush
                });
            }

            var barsRow = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            double[] ratios = { 0.72, 0.56, 0.84, 0.38 };
            foreach (double r in ratios)
            {
                var barTrack = new Border
                {
                    Width = 11.5,
                    Height = 44,
                    CornerRadius = new CornerRadius(5.5),
                    Background = new SolidColorBrush(track),
                    Margin = new Thickness(3.5, 0, 3.5, 0)
                };

                var fillGrid = new Grid
                {
                    Width = 11.5,
                    Height = Math.Round(44 * r),
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                fillGrid.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(5.5),
                    Background = new LinearGradientBrush(fillTop, fillBot, 90.0)
                });
                if (texBrush != null)
                {
                    fillGrid.Children.Add(new Border
                    {
                        CornerRadius = new CornerRadius(5.5),
                        Background = texBrush
                    });
                }

                barTrack.Child = fillGrid;
                barsRow.Children.Add(barTrack);
            }
            previewBox.Children.Add(barsRow);
            stack.Children.Add(previewBox);

            stack.Children.Add(new TextBlock
            {
                Text = AppSettings.GetColorPresetTitle(preset),
                FontSize = 11.5,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = new SolidColorBrush(isSelected ? currentPalette.ButtonActiveText : currentPalette.ButtonText),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            outerCard.Child = stack;

            var targetPreset = preset;
            outerCard.MouseLeftButtonDown += (_, args) =>
            {
                args.Handled = true;
                _workingCopy.ColorPreset = targetPreset;
                ApplyThemeAndRefreshPreview();
            };

            ThemeCardsWrapPanel.Children.Add(outerCard);
        }
    }

    private void LoadFromSettings(AppSettings s)
    {
        RbMode1.IsChecked = s.SliderMode == PieSliderMode.RadialGauge;
        RbMode2.IsChecked = s.SliderMode == PieSliderMode.RotaryDial;
        RbMode3.IsChecked = s.SliderMode == PieSliderMode.OuterArcExpand;

        ChkDirectHover.IsChecked = s.DirectHoverAdjust;
        ChkEnableHotkey.IsChecked = s.EnableKeyboardShortcut;
        ChkPassThroughHotkey.IsChecked = s.PassThroughKeyboardShortcut;
        BtnRecordHotkey.Content = s.GetHotkeyDisplayText();

        CmbMouseTrigger.SelectedIndex = Math.Clamp((int)s.MouseTrigger, 0, 4);
        SldMouseHoldDelay.Value = Math.Clamp(s.MouseHoldDelayMs, 100, 500);
        TxtMouseHoldDelay.Text = $"{(int)SldMouseHoldDelay.Value} ms";

        CmbTriggerBehavior.SelectedIndex = Math.Clamp((int)s.TriggerBehavior, 0, 2);
        if (CmbLanguageHeader != null)
        {
            CmbLanguageHeader.SelectedIndex = Math.Clamp((int)s.Language, 0, 5);
        }
        if (CmbLanguageTab != null)
        {
            CmbLanguageTab.SelectedIndex = Math.Clamp((int)s.Language, 0, 5);
        }

        CmbThemeMode.SelectedIndex = Math.Clamp((int)s.ThemeMode, 0, 2);
        if (CmbSliceLabelStyle != null)
        {
            CmbSliceLabelStyle.SelectedIndex = Math.Clamp((int)s.SliceLabelStyle, 0, 1);
        }

        if (SldMenuScale != null)
        {
            SldMenuScale.Value = Math.Clamp(s.MenuScale, 60, 160);
            TxtMenuScale.Text = $"{(int)SldMenuScale.Value} %";
        }

        ChkMasterVol.IsChecked = s.IncludeMasterVolume;
        ChkBrightness.IsChecked = s.IncludeBrightness;
        ChkMicVol.IsChecked = s.IncludeMicVolume;
        ChkAppSessions.IsChecked = s.IncludeAppSessions;
        ChkSoftwareDimmer.IsChecked = s.EnableSoftwareDimmerFallback;

        SldWheelStep.Value = Math.Clamp(s.WheelStep, 1, 10);
        TxtWheelStep.Text = $"{(int)SldWheelStep.Value} %";

        ChkRunAtStartup.IsChecked = _settingsService.IsRunAtStartupEnabled();
    }

    private void SliderMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        if (RbMode1.IsChecked == true) _workingCopy.SliderMode = PieSliderMode.RadialGauge;
        else if (RbMode2.IsChecked == true) _workingCopy.SliderMode = PieSliderMode.RotaryDial;
        else if (RbMode3.IsChecked == true) _workingCopy.SliderMode = PieSliderMode.OuterArcExpand;
        ApplyThemeAndRefreshPreview();
    }

    private void ChkDirectHover_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _workingCopy.DirectHoverAdjust = ChkDirectHover.IsChecked == true;
        _settingsService.Save(_workingCopy.Clone());
    }

    private void TargetItems_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _workingCopy.IncludeMasterVolume = ChkMasterVol.IsChecked == true;
        _workingCopy.IncludeBrightness = ChkBrightness.IsChecked == true;
        _workingCopy.IncludeMicVolume = ChkMicVol.IsChecked == true;
        _workingCopy.IncludeAppSessions = ChkAppSessions.IsChecked == true;
        RebuildPreviewPieItems();
        ApplyThemeAndRefreshPreview();
    }


    private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var combo = sender as ComboBox;
        if (combo == null || combo.SelectedIndex < 0) return;

        var selectedLang = (AppLanguage)Math.Clamp(combo.SelectedIndex, 0, 5);
        if (_workingCopy.Language == selectedLang) return;

        _workingCopy.Language = selectedLang;

        // Synchronize both language selector dropdowns
        if (CmbLanguageHeader != null && CmbLanguageHeader.SelectedIndex != (int)selectedLang)
        {
            CmbLanguageHeader.SelectedIndex = (int)selectedLang;
        }
        if (CmbLanguageTab != null && CmbLanguageTab.SelectedIndex != (int)selectedLang)
        {
            CmbLanguageTab.SelectedIndex = (int)selectedLang;
        }

        LocalizationService.ApplyLanguage(selectedLang);
        _settingsService.Save(_workingCopy.Clone());
    }

    private void CmbThemeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        _workingCopy.ThemeMode = (AppThemeMode)Math.Clamp(CmbThemeMode.SelectedIndex, 0, 2);
        ApplyThemeAndRefreshPreview();
    }

    private void CmbSliceLabelStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        _workingCopy.SliceLabelStyle = (SliceLabelStyle)Math.Clamp(CmbSliceLabelStyle.SelectedIndex, 0, 1);
        ApplyThemeAndRefreshPreview();
    }

    private void BtnRecordHotkey_Click(object sender, RoutedEventArgs e)
    {
        _isRecordingHotkey = !_isRecordingHotkey;
        _hookService.IsSuppressed = _isRecordingHotkey;

        if (_isRecordingHotkey)
        {
            BtnRecordHotkey.Content = LocalizationService.Get("Loc_HotkeyRecording");
        }
        else
        {
            RestoreHotkeyButtonAppearance();
        }
    }

    private void RestoreHotkeyButtonAppearance()
    {
        _isRecordingHotkey = false;
        _hookService.IsSuppressed = false;
        BtnRecordHotkey.Content = _workingCopy.GetHotkeyDisplayText();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_isRecordingHotkey) return;

        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            RestoreHotkeyButtonAppearance();
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        uint mods = 0;
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods |= 1;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods |= 2;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods |= 4;
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) mods |= 8;

        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk > 0)
        {
            _workingCopy.HotkeyModifiers = mods;
            _workingCopy.HotkeyVirtualKey = vk;
            _workingCopy.EnableKeyboardShortcut = true;
            ChkEnableHotkey.IsChecked = true;
        }

        RestoreHotkeyButtonAppearance();
    }

    private void SldMouseHoldDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtMouseHoldDelay != null)
        {
            TxtMouseHoldDelay.Text = $"{(int)e.NewValue} ms";
        }
        if (!_isInitializing)
        {
            _workingCopy.MouseHoldDelayMs = (int)e.NewValue;
            _settingsService.Save(_workingCopy.Clone());
        }
    }

    private void SldMenuScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtMenuScale != null)
        {
            TxtMenuScale.Text = $"{(int)e.NewValue} %";
        }
        if (!_isInitializing)
        {
            _workingCopy.MenuScale = (int)e.NewValue;
            ApplyThemeAndRefreshPreview();
            _settingsService.Save(_workingCopy.Clone());
        }
    }

    private void SldWheelStep_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtWheelStep != null)
        {
            TxtWheelStep.Text = $"{(int)e.NewValue} %";
        }
        if (!_isInitializing)
        {
            _workingCopy.WheelStep = (int)e.NewValue;
            PreviewPieCanvas.WheelStep = _workingCopy.WheelStep;

            _settingsService.Save(_workingCopy.Clone());
        }
    }

    private void TriggerSettings_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _workingCopy.EnableKeyboardShortcut = ChkEnableHotkey.IsChecked == true;
        _workingCopy.PassThroughKeyboardShortcut = ChkPassThroughHotkey.IsChecked == true;
        _workingCopy.MouseTrigger = (MouseTriggerType)Math.Clamp(CmbMouseTrigger.SelectedIndex, 0, 4);
        _workingCopy.TriggerBehavior = (TriggerBehaviorMode)Math.Clamp(CmbTriggerBehavior.SelectedIndex, 0, 2);
        _workingCopy.EnableSoftwareDimmerFallback = ChkSoftwareDimmer.IsChecked == true;
        _settingsService.Save(_workingCopy.Clone());
    }

    private void ChkRunAtStartup_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settingsService.SetRunAtStartup(ChkRunAtStartup.IsChecked == true);
    }

    private void BtnTestPie_Click(object sender, RoutedEventArgs e)
    {
        RestoreHotkeyButtonAppearance();
        _testPieAction.Invoke();
    }

    private void BtnRevert_Click(object sender, RoutedEventArgs e)
    {
        RestoreHotkeyButtonAppearance();
        _workingCopy = _initialBackup.Clone();
        _isInitializing = true;
        LoadFromSettings(_workingCopy);
        _isInitializing = false;
        LocalizationService.ApplyLanguage(_workingCopy.Language);
        RebuildPreviewPieItems();
        ApplyThemeAndRefreshPreview();
        _settingsService.Save(_workingCopy.Clone());
        _settingsService.SetRunAtStartup(_initialRunAtStartup);
    }
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        RestoreHotkeyButtonAppearance();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        RestoreHotkeyButtonAppearance();
        SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        _hookService.IsSuppressed = false;
        base.OnClosed(e);
    }

    #region DWM Window Title Bar Dark Mode
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    private void UpdateTitleBarTheme(ThemePalette p)
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;

            int useDarkMode = p.IsLight ? 0 : 1;

            // Apply dark mode caption to DWM (Windows 10 18985+ / Windows 11)
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
            // Windows 10 18362-18985 fallback
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));

            // Windows 11 Build 22000+: Set custom caption bar and title text colors matching WindowBg / TextPrimary
            // COLORREF format: 0x00BBGGRR
            int captionColor = p.WindowBg.R | (p.WindowBg.G << 8) | (p.WindowBg.B << 16);
            int textColor = p.TextPrimary.R | (p.TextPrimary.G << 8) | (p.TextPrimary.B << 16);

            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
        }
        catch
        {
            // Safe fallback if DWM calls fail or running on unsupported Windows versions
        }
    }
    #endregion
}
