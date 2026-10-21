using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VolumePie.Models;
using Color = System.Windows.Media.Color;

namespace VolumePie.Services;

public sealed class ThemePalette
{
    public bool IsLight { get; init; }
    public UiColorPreset Preset { get; init; }

    // Unified Opaque Circular Platter (recessed groove behind slices so background windows never bleed through)
    public Color PlatterBg { get; init; }
    public Color PlatterBorder { get; init; }

    // Pie Menu Canvas Colors (All 100% Opaque RGB so appearance is completely uniform regardless of background window)
    public Color SliceBg { get; init; }
    public Color SliceBgSelected { get; init; }
    public Color SliceBorder { get; init; }
    public Color SliceBorderSelected { get; init; }

    public Color GaugeFillStart { get; init; }
    public Color GaugeFillEnd { get; init; }
    public Color GaugeFillSelectedStart { get; init; }
    public Color GaugeFillSelectedEnd { get; init; }
    public Color GaugeLeadingEdge { get; init; }
    public Color GaugeMutedFill { get; init; }
    public Color GuideLine { get; init; }
    public Color TrackBackground { get; init; }

    public Color GlowColor { get; init; }

    public Color ContentPillBg { get; init; }
    public Color ContentPillBorder { get; init; }
    public Color ContentPillBorderSelected { get; init; }

    public Color TextPrimary { get; init; }
    public Color TextSecondary { get; init; }
    public Color TextMutedAlert { get; init; }

    public Color CenterHubStart { get; init; }
    public Color CenterHubEnd { get; init; }
    public Color CenterHubBorder { get; init; }
    public Color CenterHubRing { get; init; }

    public Color ThumbFill { get; init; }
    public Color ThumbBorder { get; init; }
    public Color ThumbText { get; init; }

    public Color MuteBadgeBg { get; init; }
    public Color MuteBadgeBgHover { get; init; }
    public Color MuteBadgeBgActive { get; init; }
    public Color MuteBadgeBorder { get; init; }

    // Window / Bar / Settings UI Colors
    public Color WindowBg { get; init; }
    public Color CardBg { get; init; }
    public Color CardBorder { get; init; }
    public Color ButtonBg { get; init; }
    public Color ButtonBorder { get; init; }
    public Color ButtonText { get; init; }
    public Color ButtonActiveBg { get; init; }
    public Color ButtonActiveBorder { get; init; }
    public Color ButtonActiveText { get; init; }
    public Color Accent => GaugeFillSelectedEnd;

    public static ThemePalette Dark => ThemeService.GetPalette(AppThemeMode.Dark, UiColorPreset.Monochrome);
    public static ThemePalette Light => ThemeService.GetPalette(AppThemeMode.Light, UiColorPreset.Monochrome);
}

public static class ThemeService
{
    private static readonly Dictionary<(bool isLight, bool isStrong), ImageBrush> MeshBrushCache = new();
    private static readonly Dictionary<(bool isLight, bool isStrong), ImageBrush> NoiseBrushCache = new();

    public static ThemePalette GetPalette(AppThemeMode mode, UiColorPreset preset = UiColorPreset.Monochrome)
    {
        bool useLight = mode switch
        {
            AppThemeMode.Light => true,
            AppThemeMode.Dark => false,
            _ => IsWindowsInLightMode()
        };

        return CreatePalette(useLight, preset);
    }

    public static readonly UiColorPreset[] OrderedPresets =
    {
        UiColorPreset.Monochrome,
        UiColorPreset.SageGray,
        UiColorPreset.ScarletRed,
        UiColorPreset.SunsetOrange,
        UiColorPreset.AmberGold,
        UiColorPreset.CitrusYellow,
        UiColorPreset.LimeGreen,
        UiColorPreset.ForestGreen,
        UiColorPreset.EmeraldGreen,
        UiColorPreset.MintTeal,
        UiColorPreset.TurquoiseCyan,
        UiColorPreset.ClearBlue,
        UiColorPreset.FrostBlue,
        UiColorPreset.CobaltBlue,
        UiColorPreset.DeepIndigo,
        UiColorPreset.RoyalPurple,
        UiColorPreset.LavenderPurple,
        UiColorPreset.MagentaBerry,
        UiColorPreset.CrimsonRose,
        UiColorPreset.DustyPink,
        UiColorPreset.TealRose
    };

    public static (Color Surface, Color Track, Color FillBottom, Color FillTop) GetPresetPreviewColors(UiColorPreset preset)
    {
        return preset switch
        {
            UiColorPreset.ClearBlue => (
                Color.FromRgb(215, 224, 234),
                Color.FromRgb(182, 204, 232),
                Color.FromRgb(0, 106, 255),
                Color.FromRgb(64, 158, 255)),
            UiColorPreset.TealRose => (
                Color.FromRgb(234, 210, 210),
                Color.FromRgb(236, 192, 196),
                Color.FromRgb(68, 146, 154),
                Color.FromRgb(106, 176, 184)),
            UiColorPreset.SageGray => (
                Color.FromRgb(218, 219, 218),
                Color.FromRgb(186, 194, 194),
                Color.FromRgb(92, 108, 109),
                Color.FromRgb(128, 144, 145)),
            UiColorPreset.CrimsonRose => (
                Color.FromRgb(226, 209, 213),
                Color.FromRgb(214, 166, 175),
                Color.FromRgb(181, 56, 78),
                Color.FromRgb(208, 98, 117)),
            UiColorPreset.FrostBlue => (
                Color.FromRgb(214, 220, 226),
                Color.FromRgb(182, 198, 218),
                Color.FromRgb(106, 156, 210),
                Color.FromRgb(148, 190, 234)),
            UiColorPreset.DustyPink => (
                Color.FromRgb(226, 212, 218),
                Color.FromRgb(218, 178, 190),
                Color.FromRgb(202, 109, 132),
                Color.FromRgb(224, 148, 168)),
            UiColorPreset.LavenderPurple => (
                Color.FromRgb(218, 218, 224),
                Color.FromRgb(188, 186, 204),
                Color.FromRgb(95, 90, 138),
                Color.FromRgb(135, 130, 178)),
            UiColorPreset.MintTeal => (
                Color.FromRgb(178, 188, 187),
                Color.FromRgb(144, 168, 166),
                Color.FromRgb(66, 140, 135),
                Color.FromRgb(104, 178, 173)),
            UiColorPreset.ScarletRed => (
                Color.FromRgb(234, 212, 214),
                Color.FromRgb(224, 174, 178),
                Color.FromRgb(212, 26, 42),
                Color.FromRgb(242, 74, 88)),
            UiColorPreset.SunsetOrange => (
                Color.FromRgb(236, 218, 208),
                Color.FromRgb(228, 188, 164),
                Color.FromRgb(228, 92, 16),
                Color.FromRgb(248, 136, 56)),
            UiColorPreset.AmberGold => (
                Color.FromRgb(236, 226, 206),
                Color.FromRgb(226, 204, 160),
                Color.FromRgb(218, 146, 0),
                Color.FromRgb(244, 184, 36)),
            UiColorPreset.CitrusYellow => (
                Color.FromRgb(232, 230, 204),
                Color.FromRgb(216, 212, 158),
                Color.FromRgb(186, 166, 8),
                Color.FromRgb(224, 204, 28)),
            UiColorPreset.LimeGreen => (
                Color.FromRgb(218, 228, 206),
                Color.FromRgb(188, 212, 162),
                Color.FromRgb(108, 172, 34),
                Color.FromRgb(144, 206, 68)),
            UiColorPreset.ForestGreen => (
                Color.FromRgb(210, 226, 214),
                Color.FromRgb(172, 208, 182),
                Color.FromRgb(24, 148, 62),
                Color.FromRgb(62, 186, 98)),
            UiColorPreset.EmeraldGreen => (
                Color.FromRgb(206, 226, 220),
                Color.FromRgb(166, 208, 194),
                Color.FromRgb(8, 148, 108),
                Color.FromRgb(48, 186, 146)),
            UiColorPreset.TurquoiseCyan => (
                Color.FromRgb(208, 224, 232),
                Color.FromRgb(168, 204, 220),
                Color.FromRgb(0, 144, 184),
                Color.FromRgb(46, 184, 218)),
            UiColorPreset.CobaltBlue => (
                Color.FromRgb(212, 220, 234),
                Color.FromRgb(174, 192, 224),
                Color.FromRgb(12, 82, 178),
                Color.FromRgb(54, 122, 214)),
            UiColorPreset.DeepIndigo => (
                Color.FromRgb(214, 214, 232),
                Color.FromRgb(178, 180, 216),
                Color.FromRgb(48, 52, 148),
                Color.FromRgb(86, 92, 188)),
            UiColorPreset.RoyalPurple => (
                Color.FromRgb(224, 212, 232),
                Color.FromRgb(198, 174, 216),
                Color.FromRgb(118, 36, 152),
                Color.FromRgb(158, 76, 192)),
            UiColorPreset.MagentaBerry => (
                Color.FromRgb(232, 210, 224),
                Color.FromRgb(218, 170, 200),
                Color.FromRgb(194, 24, 122),
                Color.FromRgb(228, 72, 162)),
            _ => (
                Color.FromRgb(42, 42, 48),
                Color.FromRgb(68, 68, 76),
                Color.FromRgb(140, 140, 152),
                Color.FromRgb(224, 224, 232))
        };
    }

    private static ThemePalette CreatePalette(bool isLight, UiColorPreset preset)
    {
        if (preset == UiColorPreset.Monochrome)
        {
            return isLight ? CreateMonochromeLight() : CreateMonochromeDark();
        }

        var (surfLight, trackLight, fillStart, fillEnd) = GetPresetPreviewColors(preset);

        if (isLight)
        {
            var platterBg = Darken(surfLight, 0.08);
            var borderCol = Darken(surfLight, 0.18);
            var selectedBorderCol = Darken(fillStart, 0.16);
            var textPri = Color.FromRgb(28, 32, 38);
            var textSec = Darken(fillStart, 0.25);

            return new ThemePalette
            {
                IsLight = true,
                Preset = preset,
                PlatterBg = platterBg,
                PlatterBorder = borderCol,

                SliceBg = trackLight,
                SliceBgSelected = Lighten(surfLight, 0.30),
                SliceBorder = borderCol,
                SliceBorderSelected = selectedBorderCol,

                // 100% Opaque gradient stops so background windows never bleed through
                GaugeFillStart = fillEnd,
                GaugeFillEnd = fillStart,
                GaugeFillSelectedStart = Lighten(fillEnd, 0.12),
                GaugeFillSelectedEnd = fillStart,
                GaugeLeadingEdge = Darken(fillStart, 0.20),
                GaugeMutedFill = Darken(trackLight, 0.08),
                GuideLine = Color.FromArgb(45, 0, 0, 0),
                TrackBackground = Darken(trackLight, 0.06),

                GlowColor = fillEnd,

                ContentPillBg = Lighten(surfLight, 0.65),
                ContentPillBorder = borderCol,
                ContentPillBorderSelected = selectedBorderCol,

                TextPrimary = textPri,
                TextSecondary = textSec,
                TextMutedAlert = Color.FromRgb(130, 85, 95),

                CenterHubStart = Lighten(surfLight, 0.55),
                CenterHubEnd = surfLight,
                CenterHubBorder = selectedBorderCol,
                CenterHubRing = fillStart,

                ThumbFill = fillStart,
                ThumbBorder = Color.FromRgb(255, 255, 255),
                ThumbText = Color.FromRgb(255, 255, 255),

                MuteBadgeBg = Lighten(surfLight, 0.45),
                MuteBadgeBgHover = trackLight,
                MuteBadgeBgActive = fillStart,
                MuteBadgeBorder = selectedBorderCol,

                WindowBg = Lighten(surfLight, 0.45),
                CardBg = Lighten(surfLight, 0.72),
                CardBorder = borderCol,
                ButtonBg = surfLight,
                ButtonBorder = borderCol,
                ButtonText = textPri,
                ButtonActiveBg = fillStart,
                ButtonActiveBorder = Darken(fillStart, 0.15),
                ButtonActiveText = Color.FromRgb(255, 255, 255)
            };
        }
        else
        {
            // Opaque Dark mode tinted with the Sound Assistant theme palette
            var platterBg = Mix(Color.FromRgb(16, 17, 21), surfLight, 0.08);
            var darkSurf = Mix(Color.FromRgb(28, 30, 36), surfLight, 0.15);
            var darkSurfSelected = Mix(Color.FromRgb(38, 40, 48), fillEnd, 0.22);
            var darkTrack = Mix(Color.FromRgb(22, 23, 28), trackLight, 0.14);
            var borderCol = Mix(Color.FromRgb(68, 70, 80), fillEnd, 0.32);
            var selectedBorderCol = Lighten(fillEnd, 0.35);

            return new ThemePalette
            {
                IsLight = false,
                Preset = preset,
                PlatterBg = platterBg,
                PlatterBorder = borderCol,

                SliceBg = darkSurf,
                SliceBgSelected = darkSurfSelected,
                SliceBorder = borderCol,
                SliceBorderSelected = selectedBorderCol,

                // 100% Opaque gradient stops so background windows never bleed through
                GaugeFillStart = Mix(fillStart, fillEnd, 0.35),
                GaugeFillEnd = fillEnd,
                GaugeFillSelectedStart = Mix(fillStart, fillEnd, 0.65),
                GaugeFillSelectedEnd = Lighten(fillEnd, 0.16),
                GaugeLeadingEdge = Lighten(fillEnd, 0.42),
                GaugeMutedFill = Color.FromRgb(52, 54, 62),
                GuideLine = Color.FromArgb(42, 255, 255, 255),
                TrackBackground = darkTrack,

                GlowColor = Lighten(fillEnd, 0.25),

                ContentPillBg = Mix(darkSurf, Color.FromRgb(20, 21, 26), 0.55),
                ContentPillBorder = borderCol,
                ContentPillBorderSelected = selectedBorderCol,

                TextPrimary = Color.FromRgb(244, 245, 247),
                TextSecondary = Mix(Color.FromRgb(175, 179, 189), fillEnd, 0.35),
                TextMutedAlert = Color.FromRgb(195, 165, 175),

                CenterHubStart = darkSurfSelected,
                CenterHubEnd = darkSurf,
                CenterHubBorder = selectedBorderCol,
                CenterHubRing = Lighten(fillEnd, 0.25),

                ThumbFill = Lighten(fillEnd, 0.35),
                ThumbBorder = darkSurf,
                ThumbText = Color.FromRgb(20, 22, 26),

                MuteBadgeBg = darkSurfSelected,
                MuteBadgeBgHover = fillStart,
                MuteBadgeBgActive = fillEnd,
                MuteBadgeBorder = selectedBorderCol,

                WindowBg = darkSurf,
                CardBg = Mix(darkSurf, Color.FromRgb(42, 44, 52), 0.45),
                CardBorder = borderCol,
                ButtonBg = Mix(darkSurf, Color.FromRgb(56, 58, 68), 0.48),
                ButtonBorder = borderCol,
                ButtonText = Color.FromRgb(234, 236, 240),
                ButtonActiveBg = fillEnd,
                ButtonActiveBorder = selectedBorderCol,
                ButtonActiveText = Color.FromRgb(18, 20, 24)
            };
        }
    }

    private static ThemePalette CreateMonochromeDark() => new()
    {
        IsLight = false,
        Preset = UiColorPreset.Monochrome,
        PlatterBg = Color.FromRgb(18, 18, 22),
        PlatterBorder = Color.FromRgb(68, 68, 76),

        SliceBg = Color.FromRgb(32, 32, 37),
        SliceBgSelected = Color.FromRgb(44, 44, 51),
        SliceBorder = Color.FromRgb(76, 76, 86),
        SliceBorderSelected = Color.FromRgb(218, 218, 226),

        // 100% Opaque clean monochrome tones (no dark muddy inner stops)
        GaugeFillStart = Color.FromRgb(114, 114, 124),
        GaugeFillEnd = Color.FromRgb(132, 132, 144),
        GaugeFillSelectedStart = Color.FromRgb(154, 154, 166),
        GaugeFillSelectedEnd = Color.FromRgb(176, 176, 190),
        GaugeLeadingEdge = Color.FromRgb(242, 242, 248),
        GaugeMutedFill = Color.FromRgb(52, 52, 60),
        GuideLine = Color.FromArgb(40, 255, 255, 255),
        TrackBackground = Color.FromRgb(24, 24, 28),

        GlowColor = Color.FromRgb(228, 228, 236),

        ContentPillBg = Color.FromRgb(26, 26, 31),
        ContentPillBorder = Color.FromRgb(96, 96, 108),
        ContentPillBorderSelected = Color.FromRgb(212, 212, 220),

        TextPrimary = Color.FromRgb(244, 244, 246),
        TextSecondary = Color.FromRgb(172, 172, 182),
        TextMutedAlert = Color.FromRgb(185, 185, 195),

        CenterHubStart = Color.FromRgb(42, 42, 49),
        CenterHubEnd = Color.FromRgb(26, 26, 31),
        CenterHubBorder = Color.FromRgb(168, 168, 178),
        CenterHubRing = Color.FromRgb(234, 234, 240),

        ThumbFill = Color.FromRgb(234, 234, 240),
        ThumbBorder = Color.FromRgb(39, 39, 42),
        ThumbText = Color.FromRgb(24, 24, 27),

        MuteBadgeBg = Color.FromRgb(38, 38, 44),
        MuteBadgeBgHover = Color.FromRgb(68, 68, 76),
        MuteBadgeBgActive = Color.FromRgb(92, 92, 102),
        MuteBadgeBorder = Color.FromRgb(158, 158, 168),

        WindowBg = Color.FromRgb(24, 24, 27),
        CardBg = Color.FromRgb(34, 34, 39),
        CardBorder = Color.FromRgb(63, 63, 70),
        ButtonBg = Color.FromRgb(45, 45, 52),
        ButtonBorder = Color.FromRgb(82, 82, 91),
        ButtonText = Color.FromRgb(228, 228, 231),
        ButtonActiveBg = Color.FromRgb(228, 228, 231),
        ButtonActiveBorder = Color.FromRgb(244, 244, 245),
        ButtonActiveText = Color.FromRgb(24, 24, 27)
    };

    private static ThemePalette CreateMonochromeLight() => new()
    {
        IsLight = true,
        Preset = UiColorPreset.Monochrome,
        PlatterBg = Color.FromRgb(218, 218, 225),
        PlatterBorder = Color.FromRgb(182, 182, 192),

        SliceBg = Color.FromRgb(242, 242, 246),
        SliceBgSelected = Color.FromRgb(252, 252, 255),
        SliceBorder = Color.FromRgb(194, 194, 204),
        SliceBorderSelected = Color.FromRgb(63, 63, 72),

        // 100% Opaque silver/slate gradient stops
        GaugeFillStart = Color.FromRgb(208, 208, 218),
        GaugeFillEnd = Color.FromRgb(164, 164, 178),
        GaugeFillSelectedStart = Color.FromRgb(192, 192, 204),
        GaugeFillSelectedEnd = Color.FromRgb(132, 132, 148),
        GaugeLeadingEdge = Color.FromRgb(48, 48, 56),
        GaugeMutedFill = Color.FromRgb(220, 220, 226),
        GuideLine = Color.FromArgb(45, 0, 0, 0),
        TrackBackground = Color.FromRgb(226, 226, 232),

        GlowColor = Color.FromRgb(90, 90, 105),

        ContentPillBg = Color.FromRgb(250, 250, 253),
        ContentPillBorder = Color.FromRgb(190, 190, 200),
        ContentPillBorderSelected = Color.FromRgb(63, 63, 72),

        TextPrimary = Color.FromRgb(24, 24, 27),
        TextSecondary = Color.FromRgb(82, 82, 91),
        TextMutedAlert = Color.FromRgb(113, 113, 122),

        CenterHubStart = Color.FromRgb(255, 255, 255),
        CenterHubEnd = Color.FromRgb(238, 238, 244),
        CenterHubBorder = Color.FromRgb(120, 120, 130),
        CenterHubRing = Color.FromRgb(52, 52, 60),

        ThumbFill = Color.FromRgb(39, 39, 42),
        ThumbBorder = Color.FromRgb(255, 255, 255),
        ThumbText = Color.FromRgb(255, 255, 255),

        MuteBadgeBg = Color.FromRgb(242, 242, 246),
        MuteBadgeBgHover = Color.FromRgb(220, 220, 226),
        MuteBadgeBgActive = Color.FromRgb(175, 175, 186),
        MuteBadgeBorder = Color.FromRgb(140, 140, 150),

        WindowBg = Color.FromRgb(244, 244, 246),
        CardBg = Color.FromRgb(255, 255, 255),
        CardBorder = Color.FromRgb(218, 218, 224),
        ButtonBg = Color.FromRgb(236, 236, 240),
        ButtonBorder = Color.FromRgb(195, 195, 204),
        ButtonText = Color.FromRgb(39, 39, 42),
        ButtonActiveBg = Color.FromRgb(39, 39, 42),
        ButtonActiveBorder = Color.FromRgb(24, 24, 27),
        ButtonActiveText = Color.FromRgb(255, 255, 255)
    };

    /// <summary>
    /// Returns a cached tileable brush for "メッシュ (Mesh)" or "ノイズ (Noise)" surface texture.
    /// Note: "アクリル (Acrylic)" is a smooth frosted-glass refraction shader, not a tiled pixel overlay.
    /// </summary>
    public static ImageBrush? GetTextureOverlayBrush(UiSurfaceTexture texture, bool isLight, bool isStrong)
    {
        return texture switch
        {
            UiSurfaceTexture.Mesh => GetOrCreateMeshBrush(isLight, isStrong),
            UiSurfaceTexture.Noise => GetOrCreateNoiseBrush(isLight, isStrong),
            _ => null
        };
    }

    /// <summary>
    /// Captures the desktop screen area behind the Pie Menu, downscales it, applies a multi-pass Gaussian-equivalent blur,
    /// and blends it with a balanced frosted-glass luminosity tint so it looks like real frosted glass (Glassmorphism / Acrylic)
    /// while keeping uniform readability regardless of bright/dark windows behind it.
    /// </summary>
    public static System.Windows.Media.Brush CreateDesktopBlurredAcrylicBrush(
        int screenLeftPx,
        int screenTopPx,
        int screenWidthPx,
        int screenHeightPx,
        ThemePalette palette)
    {
        try
        {
            const int blurSize = 96;
            using var screenBmp = new System.Drawing.Bitmap(Math.Max(16, screenWidthPx), Math.Max(16, screenHeightPx), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(screenBmp))
            {
                g.CopyFromScreen(screenLeftPx, screenTopPx, 0, 0, screenBmp.Size, System.Drawing.CopyPixelOperation.SourceCopy);
            }

            using var smallBmp = new System.Drawing.Bitmap(blurSize, blurSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var gSmall = System.Drawing.Graphics.FromImage(smallBmp))
            {
                gSmall.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                gSmall.DrawImage(screenBmp, new System.Drawing.Rectangle(0, 0, blurSize, blurSize));
            }

            byte[] src = new byte[blurSize * blurSize * 4];
            var rect = new System.Drawing.Rectangle(0, 0, blurSize, blurSize);
            var bmpData = smallBmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, src, 0, src.Length);
            smallBmp.UnlockBits(bmpData);

            // 3-pass separable box blur with radius 7 on 96x96 (= ~40px Gaussian blur at 540x540)
            byte[] temp = new byte[src.Length];
            for (int pass = 0; pass < 3; pass++)
            {
                BlurHorizontal(src, temp, blurSize, blurSize, 7);
                BlurVertical(temp, src, blurSize, blurSize, 7);
            }

            // Frosted-glass luminosity compression & tint blend (Dark Mode = smoked dark glass, Light Mode = milky frosted glass)
            Color tintBase = palette.Preset == UiColorPreset.Monochrome
                ? (palette.IsLight ? Color.FromRgb(228, 232, 238) : Color.FromRgb(34, 38, 46))
                : (palette.IsLight
                    ? Mix(palette.SliceBgSelected, Color.FromRgb(235, 238, 244), 0.45)
                    : Mix(palette.SliceBg, Color.FromRgb(28, 32, 40), 0.55));

            double tintWeight = palette.IsLight ? 0.62 : 0.66;

            for (int i = 0; i < blurSize * blurSize; i++)
            {
                int idx = i * 4;
                double b = src[idx + 0];
                double g = src[idx + 1];
                double r = src[idx + 2];

                if (palette.IsLight)
                {
                    // Light mode: bright milky frosted range [168..244] for crisp dark text
                    r = 168.0 + (r / 255.0) * 76.0;
                    g = 170.0 + (g / 255.0) * 76.0;
                    b = 174.0 + (b / 255.0) * 76.0;
                }
                else
                {
                    // Dark mode: deep smoked frosted glass range [24..86] for crisp white text
                    r = 24.0 + (r / 255.0) * 60.0;
                    g = 26.0 + (g / 255.0) * 62.0;
                    b = 30.0 + (b / 255.0) * 66.0;
                }

                // Blend with frosted glass tint
                byte outR = (byte)Math.Clamp(Math.Round(r * (1.0 - tintWeight) + tintBase.R * tintWeight), 0, 255);
                byte outG = (byte)Math.Clamp(Math.Round(g * (1.0 - tintWeight) + tintBase.G * tintWeight), 0, 255);
                byte outB = (byte)Math.Clamp(Math.Round(b * (1.0 - tintWeight) + tintBase.B * tintWeight), 0, 255);

                src[idx + 0] = outB;
                src[idx + 1] = outG;
                src[idx + 2] = outR;
                src[idx + 3] = 255;
            }

            var wpfBmp = BitmapSource.Create(blurSize, blurSize, 96, 96, PixelFormats.Bgra32, null, src, blurSize * 4);
            wpfBmp.Freeze();

            var imgBrush = new ImageBrush(wpfBmp)
            {
                Stretch = Stretch.Fill
            };
            imgBrush.Freeze();
            return imgBrush;
        }
        catch
        {
            return GetSimulatedAcrylicBackdropBrush(palette);
        }
    }

    private static void BlurHorizontal(byte[] src, byte[] dst, int w, int h, int radius)
    {
        int diam = radius * 2 + 1;
        for (int y = 0; y < h; y++)
        {
            int rowOffset = y * w * 4;
            for (int x = 0; x < w; x++)
            {
                int sumB = 0, sumG = 0, sumR = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int sx = Math.Clamp(x + k, 0, w - 1);
                    int idx = rowOffset + sx * 4;
                    sumB += src[idx + 0];
                    sumG += src[idx + 1];
                    sumR += src[idx + 2];
                }
                int outIdx = rowOffset + x * 4;
                dst[outIdx + 0] = (byte)(sumB / diam);
                dst[outIdx + 1] = (byte)(sumG / diam);
                dst[outIdx + 2] = (byte)(sumR / diam);
                dst[outIdx + 3] = 255;
            }
        }
    }

    private static void BlurVertical(byte[] src, byte[] dst, int w, int h, int radius)
    {
        int diam = radius * 2 + 1;
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int sumB = 0, sumG = 0, sumR = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int sy = Math.Clamp(y + k, 0, h - 1);
                    int idx = (sy * w + x) * 4;
                    sumB += src[idx + 0];
                    sumG += src[idx + 1];
                    sumR += src[idx + 2];
                }
                int outIdx = (y * w + x) * 4;
                dst[outIdx + 0] = (byte)(sumB / diam);
                dst[outIdx + 1] = (byte)(sumG / diam);
                dst[outIdx + 2] = (byte)(sumR / diam);
                dst[outIdx + 3] = 255;
            }
        }
    }

    /// <summary>
    /// Smooth multi-stop frosted glass backdrop brush (matches the creamy blurred glass look in the reference image)
    /// for use in the Settings Live Preview and Swatch Cards.
    /// </summary>
    public static LinearGradientBrush GetSimulatedAcrylicBackdropBrush(ThemePalette palette)
    {
        Color cTopLeft, cMidUpper, cMidLower, cBottomRight;
        if (palette.Preset == UiColorPreset.Monochrome)
        {
            if (palette.IsLight)
            {
                cTopLeft = Color.FromRgb(218, 222, 228);
                cMidUpper = Color.FromRgb(232, 236, 242);
                cMidLower = Color.FromRgb(194, 202, 212);
                cBottomRight = Color.FromRgb(176, 186, 198);
            }
            else
            {
                // Deep smoked frosted glass in Dark Mode so white/silver labels pop with high contrast
                cTopLeft = Color.FromRgb(48, 52, 60);
                cMidUpper = Color.FromRgb(64, 70, 80);
                cMidLower = Color.FromRgb(38, 43, 52);
                cBottomRight = Color.FromRgb(28, 32, 40);
            }
        }
        else
        {
            var (surf, track, fillBot, fillTop) = GetPresetPreviewColors(palette.Preset);
            if (palette.IsLight)
            {
                cTopLeft = Mix(Color.FromRgb(215, 220, 228), surf, 0.35);
                cMidUpper = Mix(Color.FromRgb(234, 238, 244), fillTop, 0.25);
                cMidLower = Mix(Color.FromRgb(196, 204, 214), track, 0.30);
                cBottomRight = Mix(Color.FromRgb(178, 188, 200), fillBot, 0.35);
            }
            else
            {
                cTopLeft = Mix(Color.FromRgb(44, 48, 56), surf, 0.50);
                cMidUpper = Mix(Color.FromRgb(62, 68, 78), fillTop, 0.28);
                cMidLower = Mix(Color.FromRgb(34, 38, 46), track, 0.48);
                cBottomRight = Mix(Color.FromRgb(24, 28, 36), fillBot, 0.42);
            }
        }

        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0.1, 0.0),
            EndPoint = new System.Windows.Point(0.85, 1.0)
        };
        brush.GradientStops.Add(new GradientStop(cTopLeft, 0.0));
        brush.GradientStops.Add(new GradientStop(cMidUpper, 0.34));
        brush.GradientStops.Add(new GradientStop(cMidLower, 0.72));
        brush.GradientStops.Add(new GradientStop(cBottomRight, 1.0));
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Smooth diagonal frosted glass surface reflection sheen (no noise!).
    /// </summary>
    public static LinearGradientBrush GetAcrylicGlassSheenBrush(bool isLight, bool isSelected)
    {
        var sheen = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0.0, 0.0),
            EndPoint = new System.Windows.Point(1.0, 1.0)
        };
        byte topAlpha = (byte)(isSelected ? (isLight ? 95 : 38) : (isLight ? 56 : 18));
        byte midAlpha = (byte)(isSelected ? (isLight ? 36 : 12) : (isLight ? 18 : 6));
        byte botAlpha = (byte)(isSelected ? (isLight ? 52 : 22) : (isLight ? 28 : 10));

        sheen.GradientStops.Add(new GradientStop(Color.FromArgb(topAlpha, 255, 255, 255), 0.0));
        sheen.GradientStops.Add(new GradientStop(Color.FromArgb(midAlpha, 255, 255, 255), 0.48));
        sheen.GradientStops.Add(new GradientStop(Color.FromArgb(botAlpha, 235, 242, 250), 1.0));
        sheen.Freeze();
        return sheen;
    }

    /// <summary>
    /// Milky translucent frosted glass fill brush (matches the active "Feed" capsule in the reference image).
    /// </summary>
    public static LinearGradientBrush GetAcrylicFrostedPillFillBrush(ThemePalette palette, bool isSelected)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0.0, 0.0),
            EndPoint = new System.Windows.Point(1.0, 1.0)
        };

        if (palette.Preset == UiColorPreset.Monochrome)
        {
            if (palette.IsLight)
            {
                byte a1 = (byte)(isSelected ? 120 : 82);
                byte a2 = (byte)(isSelected ? 88 : 58);
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(a1, 132, 144, 160), 0.0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(a2, 112, 124, 142), 1.0));
            }
            else
            {
                byte a1 = (byte)(isSelected ? 96 : 62);
                byte a2 = (byte)(isSelected ? 68 : 42);
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(a1, 255, 255, 255), 0.0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(a2, 224, 232, 242), 1.0));
            }
        }
        else
        {
            Color c1 = palette.IsLight ? palette.GaugeFillSelectedEnd : Lighten(palette.GaugeFillSelectedEnd, 0.25);
            Color c2 = palette.IsLight ? palette.GaugeFillEnd : Lighten(palette.GaugeFillEnd, 0.12);
            byte a1 = (byte)(isSelected ? 132 : 88);
            byte a2 = (byte)(isSelected ? 96 : 64);
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(a1, c1.R, c1.G, c1.B), 0.0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(a2, c2.R, c2.G, c2.B), 1.0));
        }

        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Crisp 1px specular glass rim brush (bright top-left & bottom-right edge catchlight like visionOS / Acrylic glass).
    /// </summary>
    public static LinearGradientBrush GetAcrylicSpecularBorderBrush(bool isSelected)
    {
        var border = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0.0, 0.0),
            EndPoint = new System.Windows.Point(1.0, 1.0)
        };
        byte hi = (byte)(isSelected ? 225 : 155);
        byte mid = (byte)(isSelected ? 95 : 55);
        byte lo = (byte)(isSelected ? 165 : 105);

        border.GradientStops.Add(new GradientStop(Color.FromArgb(hi, 255, 255, 255), 0.0));
        border.GradientStops.Add(new GradientStop(Color.FromArgb(mid, 255, 255, 255), 0.5));
        border.GradientStops.Add(new GradientStop(Color.FromArgb(lo, 255, 255, 255), 1.0));
        border.Freeze();
        return border;
    }

    private static ImageBrush GetOrCreateMeshBrush(bool isLight, bool isStrong)
    {
        var key = (isLight, isStrong);
        if (MeshBrushCache.TryGetValue(key, out var cached))
            return cached;

        // 8x8 staggered honeycomb / acoustic micro-dot speaker mesh (subtle, non-intrusive, zero grid harshness)
        const int size = 8;
        byte[] pixels = new byte[size * size * 4];

        double dimpleAlphaMax = isStrong ? (isLight ? 24.0 : 32.0) : (isLight ? 15.0 : 20.0);
        double sheenAlphaMax = isStrong ? (isLight ? 18.0 : 14.0) : (isLight ? 10.0 : 8.0);

        (double cx, double cy)[] centers =
        {
            (2.0, 2.0),
            (6.0, 6.0),
            (2.0, 10.0),
            (6.0, -2.0),
            (-2.0, 6.0),
            (10.0, 2.0)
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = (y * size + x) * 4;
                double minDist = 99.0;
                foreach (var (cx, cy) in centers)
                {
                    double dx = (x + 0.5) - cx;
                    double dy = (y + 0.5) - cy;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d < minDist) minDist = d;
                }

                if (minDist <= 1.65)
                {
                    // Soft circular acoustic micro-dimple
                    double t = 1.0 - (minDist / 1.65);
                    byte a = (byte)Math.Clamp(Math.Round(dimpleAlphaMax * (0.45 + 0.55 * t)), 0, 255);
                    pixels[idx + 0] = 0;
                    pixels[idx + 1] = 0;
                    pixels[idx + 2] = 0;
                    pixels[idx + 3] = a;
                }
                else
                {
                    // Subtle matte weave highlight between dimples
                    byte a = (byte)Math.Clamp(Math.Round(sheenAlphaMax), 0, 255);
                    pixels[idx + 0] = 255;
                    pixels[idx + 1] = 255;
                    pixels[idx + 2] = 255;
                    pixels[idx + 3] = a;
                }
            }
        }

        var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        bmp.Freeze();

        var brush = new ImageBrush(bmp)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 6.0, 6.0),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill
        };
        brush.Freeze();
        MeshBrushCache[key] = brush;
        return brush;
    }

    private static ImageBrush GetOrCreateNoiseBrush(bool isLight, bool isStrong)
    {
        var key = (isLight, isStrong);
        if (NoiseBrushCache.TryGetValue(key, out var cached))
            return cached;

        // 64x64 fine grain/noise texture
        const int size = 64;
        byte[] pixels = new byte[size * size * 4];
        var rng = new Random(1337 + (isStrong ? 99 : 0));

        int maxGrainAlpha = isStrong ? (isLight ? 36 : 46) : (isLight ? 20 : 28);

        for (int i = 0; i < size * size; i++)
        {
            int idx = i * 4;
            int v = rng.Next(-maxGrainAlpha, maxGrainAlpha + 1);
            if (v >= 0)
            {
                pixels[idx + 0] = 255;
                pixels[idx + 1] = 255;
                pixels[idx + 2] = 255;
                pixels[idx + 3] = (byte)v;
            }
            else
            {
                pixels[idx + 0] = 0;
                pixels[idx + 1] = 0;
                pixels[idx + 2] = 0;
                pixels[idx + 3] = (byte)(-v);
            }
        }

        var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        bmp.Freeze();

        var brush = new ImageBrush(bmp)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 48, 48),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill
        };
        brush.Freeze();
        NoiseBrushCache[key] = brush;
        return brush;
    }

    private static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    private static Color Lighten(Color c, double t) => Mix(c, Color.FromRgb(255, 255, 255), t);
    private static Color Darken(Color c, double t) => Mix(c, Color.FromRgb(0, 0, 0), t);

    public static bool IsWindowsInLightMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
            object? val = key?.GetValue("AppsUseLightTheme");
            if (val is int intVal)
            {
                return intVal != 0;
            }
        }
        catch
        {
        }
        return false;
    }
}
