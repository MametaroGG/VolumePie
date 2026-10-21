using System;
using System.Text.Json.Serialization;
using VolumePie.Services;

namespace VolumePie.Models;

public enum PieSliderMode
{
    /// <summary>
    /// 候補1: 各扇形が中心から外側へ向かうラジアルゲージになっており、外側への距離で0〜100%を調整
    /// </summary>
    RadialGauge = 0,

    /// <summary>
    /// 候補2: 扇形で項目を選択し、円周方向（時計回り・反時計回り）のドラッグでダイヤル調整
    /// </summary>
    RotaryDial = 1,

    /// <summary>
    /// 候補3: 扇形を選択すると外周に円弧（アーク）スライダーが展開され、それに沿ってドラッグ調整
    /// </summary>
    OuterArcExpand = 2
}

public enum MouseTriggerType
{
    None = 0,
    MiddleButtonHold = 1,
    XButton1Hold = 2,
    XButton2Hold = 3,
    ShiftPlusRightClick = 4
}

public enum TriggerBehaviorMode
{
    /// <summary>
    /// 短押し時はトグル表示、長押し操作時はキー/ボタンを離すと閉じる
    /// </summary>
    SmartHybrid = 0,
    HoldOnly = 1,
    ToggleOnly = 2
}

public enum SliceLabelStyle
{
    IconOnly = 0,
    IconAndText = 1
}

public enum AppThemeMode
{
    System = 0,
    Light = 1,
    Dark = 2
}

public enum UiSurfaceTexture
{
    Mesh = 0,
    Noise = 1,
    Flat = 2,
    Acrylic = 3
}

public enum UiLightingEffect
{
    SoftGlow = 0,
    EdgeRimGlow = 1,
    PrismAurora = 2,
    Minimal = 3
}

public enum UiColorPreset
{
    Monochrome = 0,
    ClearBlue = 1,
    TealRose = 2,
    SageGray = 3,
    CrimsonRose = 4,
    FrostBlue = 5,
    DustyPink = 6,
    LavenderPurple = 7,
    MintTeal = 8,
    ScarletRed = 9,
    SunsetOrange = 10,
    AmberGold = 11,
    CitrusYellow = 12,
    LimeGreen = 13,
    ForestGreen = 14,
    EmeraldGreen = 15,
    TurquoiseCyan = 16,
    CobaltBlue = 17,
    DeepIndigo = 18,
    RoyalPurple = 19,
    MagentaBerry = 20
}

public enum AppLanguage
{
    System = 0,
    Japanese = 1,
    English = 2,
    Chinese = 3,              // 简体中文
    ChineseTraditional = 4,   // 繁體中文
    Korean = 5                // 한국어
}

public class AppSettings
{
    public AppLanguage Language { get; set; } = AppLanguage.System;
    public PieSliderMode SliderMode { get; set; } = PieSliderMode.RadialGauge;
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.System;
    public UiSurfaceTexture SurfaceTexture { get; set; } = UiSurfaceTexture.Flat;
    public UiLightingEffect LightingEffect { get; set; } = UiLightingEffect.Minimal;
    public UiColorPreset ColorPreset { get; set; } = UiColorPreset.Monochrome;
    public SliceLabelStyle SliceLabelStyle { get; set; } = SliceLabelStyle.IconOnly;
    public int MenuScale { get; set; } = 85;

    /// <summary>
    /// trueの場合、スライダーをドラッグして離した瞬間に自動でパイメニューを閉じる
    /// </summary>
    public bool DirectHoverAdjust { get; set; } = false;

    public bool EnableKeyboardShortcut { get; set; } = true;
    public bool PassThroughKeyboardShortcut { get; set; } = false;

    /// <summary>
    /// Modifiers: 1 = Alt, 2 = Ctrl, 4 = Shift, 8 = Win
    /// </summary>
    public uint HotkeyModifiers { get; set; } = 1; // Alt

    /// <summary>
    /// Win32 Virtual Key Code (0x51 = 'Q')
    /// </summary>
    public int HotkeyVirtualKey { get; set; } = 0x51; // Q

    public MouseTriggerType MouseTrigger { get; set; } = MouseTriggerType.MiddleButtonHold;

    public int MouseHoldDelayMs { get; set; } = 180;

    public TriggerBehaviorMode TriggerBehavior { get; set; } = TriggerBehaviorMode.SmartHybrid;

    public int WheelStep { get; set; } = 3;

    public bool IncludeMasterVolume { get; set; } = true;
    public bool IncludeMicVolume { get; set; } = true;
    public bool IncludeBrightness { get; set; } = true;
    public bool IncludeAppSessions { get; set; } = true;
    public int MaxAppSlices { get; set; } = 6;
    public bool EnableSoftwareDimmerFallback { get; set; } = true;

    public AppSettings Clone()
    {
        return (AppSettings)MemberwiseClone();
    }

    public string GetHotkeyDisplayText()
    {
        if (!EnableKeyboardShortcut || HotkeyVirtualKey <= 0)
            return LocalizationService.Get("Loc_HotkeyDisabled");

        var parts = new System.Collections.Generic.List<string>();
        if ((HotkeyModifiers & 2) != 0) parts.Add("Ctrl");
        if ((HotkeyModifiers & 1) != 0) parts.Add("Alt");
        if ((HotkeyModifiers & 4) != 0) parts.Add("Shift");
        if ((HotkeyModifiers & 8) != 0) parts.Add("Win");

        var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey(HotkeyVirtualKey);
        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }

    public static string GetSliderModeTitle(PieSliderMode mode) => mode switch
    {
        PieSliderMode.RadialGauge => LocalizationService.Get("Loc_SliderMode1Title"),
        PieSliderMode.RotaryDial => LocalizationService.Get("Loc_SliderMode2Title"),
        PieSliderMode.OuterArcExpand => LocalizationService.Get("Loc_SliderMode3Title"),
        _ => mode.ToString()
    };

    public static string GetSliderModeDescription(PieSliderMode mode) => mode switch
    {
        PieSliderMode.RadialGauge => LocalizationService.Get("Loc_SliderMode1Desc"),
        PieSliderMode.RotaryDial => LocalizationService.Get("Loc_SliderMode2Desc"),
        PieSliderMode.OuterArcExpand => LocalizationService.Get("Loc_SliderMode3Desc"),
        _ => ""
    };

    public static string GetTextureTitle(UiSurfaceTexture tex) => tex switch
    {
        UiSurfaceTexture.Flat => LocalizationService.Get("Loc_TextureFlat"),
        UiSurfaceTexture.Acrylic => LocalizationService.Get("Loc_TextureAcrylic"),
        UiSurfaceTexture.Mesh => LocalizationService.Get("Loc_TextureMesh"),
        UiSurfaceTexture.Noise => LocalizationService.Get("Loc_TextureNoise"),
        _ => tex.ToString()
    };

    public static string GetLightingTitle(UiLightingEffect light) => light switch
    {
        UiLightingEffect.Minimal => LocalizationService.Get("Loc_LightingMinimal"),
        UiLightingEffect.SoftGlow => LocalizationService.Get("Loc_LightingSoftGlow"),
        UiLightingEffect.EdgeRimGlow => LocalizationService.Get("Loc_LightingEdgeRimGlow"),
        UiLightingEffect.PrismAurora => LocalizationService.Get("Loc_LightingPrismAurora"),
        _ => light.ToString()
    };

    public static string GetColorPresetTitle(UiColorPreset preset) => preset switch
    {
        UiColorPreset.Monochrome => LocalizationService.Get("Loc_ColorMonochrome"),
        UiColorPreset.ClearBlue => LocalizationService.Get("Loc_ColorClearBlue"),
        UiColorPreset.TealRose => LocalizationService.Get("Loc_ColorTealRose"),
        UiColorPreset.SageGray => LocalizationService.Get("Loc_ColorSageGray"),
        UiColorPreset.CrimsonRose => LocalizationService.Get("Loc_ColorCrimsonRose"),
        UiColorPreset.FrostBlue => LocalizationService.Get("Loc_ColorFrostBlue"),
        UiColorPreset.DustyPink => LocalizationService.Get("Loc_ColorDustyPink"),
        UiColorPreset.LavenderPurple => LocalizationService.Get("Loc_ColorLavenderPurple"),
        UiColorPreset.MintTeal => LocalizationService.Get("Loc_ColorMintTeal"),
        UiColorPreset.ScarletRed => LocalizationService.Get("Loc_ColorScarletRed"),
        UiColorPreset.SunsetOrange => LocalizationService.Get("Loc_ColorSunsetOrange"),
        UiColorPreset.AmberGold => LocalizationService.Get("Loc_ColorAmberGold"),
        UiColorPreset.CitrusYellow => LocalizationService.Get("Loc_ColorCitrusYellow"),
        UiColorPreset.LimeGreen => LocalizationService.Get("Loc_ColorLimeGreen"),
        UiColorPreset.ForestGreen => LocalizationService.Get("Loc_ColorForestGreen"),
        UiColorPreset.EmeraldGreen => LocalizationService.Get("Loc_ColorEmeraldGreen"),
        UiColorPreset.TurquoiseCyan => LocalizationService.Get("Loc_ColorTurquoiseCyan"),
        UiColorPreset.CobaltBlue => LocalizationService.Get("Loc_ColorCobaltBlue"),
        UiColorPreset.DeepIndigo => LocalizationService.Get("Loc_ColorDeepIndigo"),
        UiColorPreset.RoyalPurple => LocalizationService.Get("Loc_ColorRoyalPurple"),
        UiColorPreset.MagentaBerry => LocalizationService.Get("Loc_ColorMagentaBerry"),
        _ => preset.ToString()
    };
}
