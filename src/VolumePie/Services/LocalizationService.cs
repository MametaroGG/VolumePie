using System;

using System.Collections.Generic;

using System.Globalization;

using System.Windows;

using VolumePie.Models;



namespace VolumePie.Services;



public static class LocalizationService

{

    public static event Action? LanguageChanged;



    private static AppLanguage _currentSetting = AppLanguage.System;

    private static bool _isJapanese = true;



    public static AppLanguage CurrentSetting => _currentSetting;

    public static bool IsJapanese => _isJapanese;



    private static readonly Dictionary<string, string> Ja = new()

    {

        // Window & Header

        ["Loc_WindowTitle"] = "VolumePie カスタマイズスタジオ",

        ["Loc_BtnTestOverlay"] = "実際のオーバーレイを表示",

        ["Loc_Language"] = "言語",

        ["Loc_LanguageTitle"] = "表示言語",

        ["Loc_LanguageSubtitle"] = "UI の表示言語を切り替えます",

        ["Loc_LanguageSystem"] = "システム",

        ["Loc_LanguageJapanese"] = "日本語",

        ["Loc_LanguageEnglish"] = "English",

        ["Loc_LanguageChinese"] = "简体中文",

        ["Loc_LanguageChineseTraditional"] = "繁體中文",

        ["Loc_LanguageKorean"] = "한국어",



        // Live Preview

        ["Loc_PreviewTitle"] = "ライブ・プレビュー",

        ["Loc_PreviewHint"] = "クリック / ドラッグ / ホイールで操作感を体験できます",

        ["Loc_PreviewBackdropLight"] = "背面: ライト",

        ["Loc_PreviewBackdropDark"] = "背面: ダーク",

        ["Loc_PreviewDetailTitle"] = "質感・ライティング拡大表示",

        ["Loc_PreviewItemMasterVolume"] = "マスター音量",

        ["Loc_PreviewItemSpeakerOutput"] = "スピーカー出力",

        ["Loc_PreviewItemBrightness"] = "画面の明るさ",

        ["Loc_PreviewItemDisplayBrightness"] = "ディスプレイ輝度",

        ["Loc_PreviewItemMicVolume"] = "マイク音量",

        ["Loc_PreviewItemMicInput"] = "マイク入力",

        ["Loc_PreviewItemAppVolume"] = "アプリ音量",

        ["Loc_PreviewItemBrowser"] = "ブラウザ",

        ["Loc_PreviewItemMusic"] = "ミュージック",



        // Tabs

        ["Loc_TabAppearance"] = "外観・質感・テーマ",

        ["Loc_TabSliderMode"] = "スライダー形状・操作感",

        ["Loc_TabTriggers"] = "トリガー・表示項目",



        // Tab 1: Appearance

        ["Loc_SurfaceAndLightingTitle"] = "表面テクスチャ ＆ ライティング効果",

        ["Loc_TextureTitle"] = "テクスチャ",

        ["Loc_LightingTitle"] = "ライティング効果",

        ["Loc_TextureFlat"] = "フラット",

        ["Loc_TextureAcrylic"] = "アクリル",

        ["Loc_TextureMesh"] = "メッシュ",

        ["Loc_TextureNoise"] = "ノイズ",

        ["Loc_LightingMinimal"] = "ミニマル",

        ["Loc_LightingSoftGlow"] = "ソフト発光",

        ["Loc_LightingEdgeRimGlow"] = "エッジ発光",

        ["Loc_LightingPrismAurora"] = "オーロラ反射",

        ["Loc_SliceStyleTitle"] = "スライスの表示スタイル",

        ["Loc_SliceStyleIconOnly"] = "アイコンのみ",

        ["Loc_SliceStyleIconAndText"] = "アイコン ＋ 名前",

        ["Loc_MenuScaleTitle"] = "メニューサイズ",

        ["Loc_ColorThemeTitle"] = "カラーテーマ ＆ 明暗モード",

        ["Loc_ThemeSystem"] = "システム",

        ["Loc_ThemeLight"] = "ライトモード",

        ["Loc_ThemeDark"] = "ダークモード",



        // Color Presets

        ["Loc_ColorMonochrome"] = "モノクローム",

        ["Loc_ColorClearBlue"] = "クリアブルー",

        ["Loc_ColorTealRose"] = "ティール＆ローズ",

        ["Loc_ColorSageGray"] = "セージグレー",

        ["Loc_ColorCrimsonRose"] = "クリムゾンローズ",

        ["Loc_ColorFrostBlue"] = "フロストブルー",

        ["Loc_ColorDustyPink"] = "ダスティピンク",

        ["Loc_ColorLavenderPurple"] = "ラベンダーパープル",

        ["Loc_ColorMintTeal"] = "ミントティール",

        ["Loc_ColorScarletRed"] = "スカーレットレッド",

        ["Loc_ColorSunsetOrange"] = "サンセットオレンジ",

        ["Loc_ColorAmberGold"] = "アンバーゴールド",

        ["Loc_ColorCitrusYellow"] = "シトラスイエロー",

        ["Loc_ColorLimeGreen"] = "ライムグリーン",

        ["Loc_ColorForestGreen"] = "フォレストグリーン",

        ["Loc_ColorEmeraldGreen"] = "エメラルドグリーン",

        ["Loc_ColorTurquoiseCyan"] = "ターコイズシアン",

        ["Loc_ColorCobaltBlue"] = "コバルトブルー",

        ["Loc_ColorDeepIndigo"] = "ディープインディゴ",

        ["Loc_ColorRoyalPurple"] = "ロイヤルパープル",

        ["Loc_ColorMagentaBerry"] = "マゼンタベリー",



        // Tab 2: Slider Mode

        ["Loc_SliderStyleTitle"] = "スライダーパイメニューの操作スタイル",

        ["Loc_SliderMode1Title"] = "ラジアルゲージ型",

        ["Loc_SliderMode1Desc"] = "各扇形そのものが内側から外側へ満ちるゲージになります。中心から外側への距離やホイール回転で 0〜100% を直感的に調整します。",

        ["Loc_SliderMode2Title"] = "円周ダイヤル型",

        ["Loc_SliderMode2Desc"] = "扇形を選択し、円の中心の周りを時計回り・反時計回りになぞるようにドラッグしてダイヤル調整します。",

        ["Loc_SliderMode3Title"] = "外周アーク展開型",

        ["Loc_SliderMode3Desc"] = "扇形を選択すると外周に円弧スライダーが展開され、外周のカーブに沿ってつまみをスライドして調整します。",

        ["Loc_SliderOptionTitle"] = "スライダーの操作感オプション",

        ["Loc_DirectHover"] = "スライダーをドラッグしてマウスボタンを離した瞬間にパイメニューを自動で閉じる",

        ["Loc_WheelStepTitle"] = "マウスホイール1目盛りの変化量",



        // Tab 3: Triggers & Items

        ["Loc_TriggerTitle"] = "呼び出しトリガー設定",

        ["Loc_HotkeyLabel"] = "キーボードショートカット",

        ["Loc_HotkeyRecording"] = "キーを入力してください... (Escでキャンセル)",

        ["Loc_HotkeyDisabled"] = "無効",
        ["Loc_PassThroughHotkey"] = "キー入力を他のアプリにも渡す（ショートカットを上書き/ブロックしない）",
        ["Loc_PassThroughHotkeyDesc"] = "有効にすると、既存ソフトが同じショートカットを使用していてもキー入力を横取りせず、背面のソフトにもキーを送信します。",

        ["Loc_MouseTriggerTitle"] = "マウスボタントリガー",

        ["Loc_MouseTriggerNone"] = "無効",

        ["Loc_MouseTriggerMiddleHold"] = "ホイールクリック長押し",

        ["Loc_MouseTriggerX1Hold"] = "マウスサイドボタン1 長押し",

        ["Loc_MouseTriggerX2Hold"] = "マウスサイドボタン2 長押し",

        ["Loc_MouseTriggerShiftRight"] = "Shift + 右クリック",

        ["Loc_MouseDelayTitle"] = "マウス長押し判定時間",

        ["Loc_TriggerBehaviorTitle"] = "開閉の動作",

        ["Loc_BehaviorSmart"] = "スマート（短く押す＝開いたまま / 長押し＝離すと閉じる）",

        ["Loc_BehaviorHold"] = "ホールド（押している間だけ表示）",

        ["Loc_BehaviorToggle"] = "トグル（押すたびに開く / 閉じる）",

        ["Loc_TargetItemsTitle"] = "パイメニューに表示する項目 ＆ システム設定",

        ["Loc_ItemMasterVolume"] = "マスター音量",

        ["Loc_ItemBrightness"] = "画面の明るさ",

        ["Loc_ItemMicVolume"] = "マイク入力音量",

        ["Loc_ItemAppSessions"] = "アプリ別音量ミキサー",

        ["Loc_SoftwareDimmer"] = "ハードウェア輝度制御に非対応のモニターではソフトウェア調光オーバーレイを使う",

        ["Loc_RunAtStartup"] = "Windows 起動時に VolumePie を自動起動する",



        // Footer

        ["Loc_BtnRevert"] = "変更前に戻す",

        ["Loc_BtnClose"] = "閉じる",



        // Tray Menu & Overlays

        ["Loc_TrayOpenPie"] = "パイメニューを開く",

        ["Loc_TrayOutputDevice"] = "出力デバイス切替",
        ["Loc_TrayInputDevice"] = "入力デバイス切替",

        ["Loc_TraySettings"] = "設定を開く...",

        ["Loc_TrayExit"] = "終了",

        ["Loc_SystemSounds"] = "システム音",

        ["Loc_MoreApps"] = "その他アプリ",
        // Pie Overlay & Center Hub
        ["Loc_PieMasterVolume"] = "マスター音量",
        ["Loc_PieBrightness"] = "画面の明るさ",
        ["Loc_PieMicVolume"] = "マイク音量",
        ["Loc_PieMicInput"] = "マイク入力",
        ["Loc_PieSystemSounds"] = "システム音",
        ["Loc_PieWindowsSounds"] = "Windows システム音",
        ["Loc_PiePlayingApp"] = "再生中アプリ",
        ["Loc_PieAppMixer"] = "アプリ一覧",
        ["Loc_PieAppsCountSuffix"] = "個のアプリ ▶",
        ["Loc_PieAppsListBadge"] = "一覧 ▶",
        ["Loc_PieAppVolume"] = "アプリ音量",
        ["Loc_PieClickToOpenApps"] = "クリックして一覧表示",
        ["Loc_PieBack"] = "◀ 戻る",
        ["Loc_PieAppMixerTitle"] = "アプリ音量一覧",
        ["Loc_PieClickToReturnMain"] = "クリックでメインへ",
        ["Loc_PieCloseHint"] = "中央クリックで閉じる",
        ["Loc_PieBackHint"] = "右クリックで戻る",
        ["Loc_PieConfirmHint"] = "ボタンを離して確定",
        ["Loc_PieRadialAdjustHint"] = "ドラッグ / ホイールで調整",
        ["Loc_PieRotaryAdjustHint"] = "円周ドラッグで調整",
        ["Loc_PieArcAdjustHint"] = "外周バーをドラッグ",
        ["Loc_PieOutputDeviceLabel"] = "出力:",
        ["Loc_PieOutputDeviceBarTooltip"] = "クリックで出力デバイス一覧を表示 / ホイール回転で即座に切替",
        ["Loc_PieInputDeviceLabel"] = "入力:",
        ["Loc_PieInputDeviceBarTooltip"] = "クリックで入力デバイス一覧を表示 / ホイール回転で即座に切替",
        ["Loc_PieDevicePopupTitle"] = "オーディオデバイス",
        ["Loc_PieDevicePopupOutputTab"] = "出力 (スピーカー)",
        ["Loc_PieDevicePopupInputTab"] = "入力 (マイク)",
        ["Loc_PieDevicePopupHint"] = "クリックで切替",
        ["Loc_PieNoOutputDevices"] = "利用可能な出力デバイスが見つかりません",
        ["Loc_PieNoInputDevices"] = "利用可能な入力デバイスが見つかりません",
        ["Loc_ModeRadial"] = "ラジアル型",
        ["Loc_ModeRotary"] = "ダイヤル型",
        ["Loc_ModeArc"] = "外周アーク型",
    };




    private static readonly Dictionary<string, string> En = new()

    {

        // Window & Header

        ["Loc_WindowTitle"] = "VolumePie Customization Studio",

        ["Loc_BtnTestOverlay"] = "Show Live Overlay",

        ["Loc_Language"] = "Language",

        ["Loc_LanguageTitle"] = "Display Language",

        ["Loc_LanguageSubtitle"] = "Switch the interface display language",

        ["Loc_LanguageSystem"] = "System",

        ["Loc_LanguageJapanese"] = "日本語",

        ["Loc_LanguageEnglish"] = "English",

        ["Loc_LanguageChinese"] = "简体中文",

        ["Loc_LanguageChineseTraditional"] = "繁體中文",

        ["Loc_LanguageKorean"] = "한국어",



        // Live Preview

        ["Loc_PreviewTitle"] = "Live Preview",

        ["Loc_PreviewHint"] = "Click, drag, or scroll to test the interaction",

        ["Loc_PreviewBackdropLight"] = "Backdrop: Light",

        ["Loc_PreviewBackdropDark"] = "Backdrop: Dark",

        ["Loc_PreviewDetailTitle"] = "Texture & Lighting Preview",

        ["Loc_PreviewItemMasterVolume"] = "Master Volume",

        ["Loc_PreviewItemSpeakerOutput"] = "Speaker Output",

        ["Loc_PreviewItemBrightness"] = "Brightness",

        ["Loc_PreviewItemDisplayBrightness"] = "Display Brightness",

        ["Loc_PreviewItemMicVolume"] = "Microphone",

        ["Loc_PreviewItemMicInput"] = "Mic Input",

        ["Loc_PreviewItemAppVolume"] = "App Volume",

        ["Loc_PreviewItemBrowser"] = "Browser",

        ["Loc_PreviewItemMusic"] = "Music",



        // Tabs

        ["Loc_TabAppearance"] = "Appearance & Theme",

        ["Loc_TabSliderMode"] = "Slider & Behavior",

        ["Loc_TabTriggers"] = "Triggers & Items",



        // Tab 1: Appearance

        ["Loc_SurfaceAndLightingTitle"] = "Surface Texture & Lighting Effects",

        ["Loc_TextureTitle"] = "Texture",

        ["Loc_LightingTitle"] = "Lighting Effect",

        ["Loc_TextureFlat"] = "Flat",

        ["Loc_TextureAcrylic"] = "Acrylic",

        ["Loc_TextureMesh"] = "Mesh",

        ["Loc_TextureNoise"] = "Noise",

        ["Loc_LightingMinimal"] = "Minimal",

        ["Loc_LightingSoftGlow"] = "Soft Glow",

        ["Loc_LightingEdgeRimGlow"] = "Edge Glow",

        ["Loc_LightingPrismAurora"] = "Prism Aurora",

        ["Loc_SliceStyleTitle"] = "Slice Display Style",

        ["Loc_SliceStyleIconOnly"] = "Icon Only",

        ["Loc_SliceStyleIconAndText"] = "Icon & Name",

        ["Loc_MenuScaleTitle"] = "Menu Size",

        ["Loc_ColorThemeTitle"] = "Color Theme & Mode",

        ["Loc_ThemeSystem"] = "System",

        ["Loc_ThemeLight"] = "Light Mode",

        ["Loc_ThemeDark"] = "Dark Mode",



        // Color Presets

        ["Loc_ColorMonochrome"] = "Monochrome",

        ["Loc_ColorClearBlue"] = "Clear Blue",

        ["Loc_ColorTealRose"] = "Teal & Rose",

        ["Loc_ColorSageGray"] = "Sage Gray",

        ["Loc_ColorCrimsonRose"] = "Crimson Rose",

        ["Loc_ColorFrostBlue"] = "Frost Blue",

        ["Loc_ColorDustyPink"] = "Dusty Pink",

        ["Loc_ColorLavenderPurple"] = "Lavender Purple",

        ["Loc_ColorMintTeal"] = "Mint Teal",

        ["Loc_ColorScarletRed"] = "Scarlet Red",

        ["Loc_ColorSunsetOrange"] = "Sunset Orange",

        ["Loc_ColorAmberGold"] = "Amber Gold",

        ["Loc_ColorCitrusYellow"] = "Citrus Yellow",

        ["Loc_ColorLimeGreen"] = "Lime Green",

        ["Loc_ColorForestGreen"] = "Forest Green",

        ["Loc_ColorEmeraldGreen"] = "Emerald Green",

        ["Loc_ColorTurquoiseCyan"] = "Turquoise Cyan",

        ["Loc_ColorCobaltBlue"] = "Cobalt Blue",

        ["Loc_ColorDeepIndigo"] = "Deep Indigo",

        ["Loc_ColorRoyalPurple"] = "Royal Purple",

        ["Loc_ColorMagentaBerry"] = "Magenta Berry",



        // Tab 2: Slider Mode

        ["Loc_SliderStyleTitle"] = "Slider Pie Menu Style",

        ["Loc_SliderMode1Title"] = "Radial Gauge",

        ["Loc_SliderMode1Desc"] = "Each slice acts as a radial gauge filling from inside to outside. Adjust 0-100% intuitively via distance from center or mouse wheel.",

        ["Loc_SliderMode2Title"] = "Rotary Dial",

        ["Loc_SliderMode2Desc"] = "Select a slice and drag clockwise or counter-clockwise around the center to adjust like a dial.",

        ["Loc_SliderMode3Title"] = "Outer Arc Expand",

        ["Loc_SliderMode3Desc"] = "Selecting a slice expands an arc slider on the outer rim. Drag along the curve to adjust.",

        ["Loc_SliderOptionTitle"] = "Slider Interaction Options",

        ["Loc_DirectHover"] = "Automatically close pie menu upon releasing mouse button after dragging",

        ["Loc_WheelStepTitle"] = "Mouse Wheel Step Change",



        // Tab 3: Triggers & Items

        ["Loc_TriggerTitle"] = "Activation Triggers",

        ["Loc_HotkeyLabel"] = "Keyboard Shortcut",

        ["Loc_HotkeyRecording"] = "Press keys... (Esc to cancel)",

        ["Loc_HotkeyDisabled"] = "Disabled",
        ["Loc_PassThroughHotkey"] = "Pass shortcut through to active apps (do not block/override)",
        ["Loc_PassThroughHotkeyDesc"] = "When enabled, VolumePie does not swallow the key event, allowing underlying apps to receive the shortcut normally.",

        ["Loc_MouseTriggerTitle"] = "Mouse Button Trigger",

        ["Loc_MouseTriggerNone"] = "Disabled",

        ["Loc_MouseTriggerMiddleHold"] = "Middle Button Hold",

        ["Loc_MouseTriggerX1Hold"] = "Side Button 1 Hold",

        ["Loc_MouseTriggerX2Hold"] = "Side Button 2 Hold",

        ["Loc_MouseTriggerShiftRight"] = "Shift + Right Click",

        ["Loc_MouseDelayTitle"] = "Mouse Hold Threshold",

        ["Loc_TriggerBehaviorTitle"] = "Open / Close Behavior",

        ["Loc_BehaviorSmart"] = "Smart (Click to keep open / Hold & release to close)",

        ["Loc_BehaviorHold"] = "Hold (Show only while held down)",

        ["Loc_BehaviorToggle"] = "Toggle (Click to open / close)",

        ["Loc_TargetItemsTitle"] = "Pie Menu Items & System Settings",

        ["Loc_ItemMasterVolume"] = "Master Volume",

        ["Loc_ItemBrightness"] = "Screen Brightness",

        ["Loc_ItemMicVolume"] = "Microphone Volume",

        ["Loc_ItemAppSessions"] = "App Volume Mixer",

        ["Loc_SoftwareDimmer"] = "Use software dimmer overlay on monitors that do not support hardware DDC/CI brightness",

        ["Loc_RunAtStartup"] = "Start VolumePie automatically on Windows startup",



        // Footer

        ["Loc_BtnRevert"] = "Revert Changes",

        ["Loc_BtnClose"] = "Close",



        // Tray Menu & Overlays

        ["Loc_TrayOpenPie"] = "Open Pie Menu",

        ["Loc_TrayOutputDevice"] = "Select Output Device",
        ["Loc_TrayInputDevice"] = "Select Input Device",

        ["Loc_TraySettings"] = "Settings...",

        ["Loc_TrayExit"] = "Exit",

        ["Loc_SystemSounds"] = "System Sounds",

        ["Loc_MoreApps"] = "More Apps",
        // Pie Overlay & Center Hub
        ["Loc_PieMasterVolume"] = "Master Volume",
        ["Loc_PieBrightness"] = "Brightness",
        ["Loc_PieMicVolume"] = "Microphone",
        ["Loc_PieMicInput"] = "Mic Input",
        ["Loc_PieSystemSounds"] = "System Sounds",
        ["Loc_PieWindowsSounds"] = "Windows Sounds",
        ["Loc_PiePlayingApp"] = "Now Playing",
        ["Loc_PieAppMixer"] = "App Mixer",
        ["Loc_PieAppsCountSuffix"] = "apps ▶",
        ["Loc_PieAppsListBadge"] = "Apps ▶",
        ["Loc_PieAppVolume"] = "App Volume",
        ["Loc_PieClickToOpenApps"] = "Click to open mixer",
        ["Loc_PieBack"] = "◀ Back",
        ["Loc_PieAppMixerTitle"] = "App Volume Mixer",
        ["Loc_PieClickToReturnMain"] = "Click to return to main",
        ["Loc_PieCloseHint"] = "Center click to close",
        ["Loc_PieBackHint"] = "Right click to go back",
        ["Loc_PieConfirmHint"] = "Release to confirm",
        ["Loc_PieRadialAdjustHint"] = "Drag / Scroll to adjust",
        ["Loc_PieRotaryAdjustHint"] = "Rotate / Scroll to adjust",
        ["Loc_PieArcAdjustHint"] = "Drag outer arc",
        ["Loc_PieOutputDeviceLabel"] = "Output:",
        ["Loc_PieOutputDeviceBarTooltip"] = "Click for output device list / Scroll to switch",
        ["Loc_PieInputDeviceLabel"] = "Input:",
        ["Loc_PieInputDeviceBarTooltip"] = "Click for input device list / Scroll to switch",
        ["Loc_PieDevicePopupTitle"] = "Audio Devices",
        ["Loc_PieDevicePopupOutputTab"] = "Output (Speaker)",
        ["Loc_PieDevicePopupInputTab"] = "Input (Mic)",
        ["Loc_PieDevicePopupHint"] = "Click to switch",
        ["Loc_PieNoOutputDevices"] = "No output devices found",
        ["Loc_PieNoInputDevices"] = "No input devices found",
        ["Loc_ModeRadial"] = "Radial",
        ["Loc_ModeRotary"] = "Dial",
        ["Loc_ModeArc"] = "Arc Slider",
    };




    private static readonly Dictionary<string, string> Zh = new()

    {

        // Window & Header

        ["Loc_WindowTitle"] = "VolumePie 自定义工作室",

        ["Loc_BtnTestOverlay"] = "显示实时悬浮窗",

        ["Loc_Language"] = "语言",

        ["Loc_LanguageTitle"] = "显示语言",

        ["Loc_LanguageSubtitle"] = "切换界面显示语言",

        ["Loc_LanguageSystem"] = "系统",

        ["Loc_LanguageJapanese"] = "日本語",

        ["Loc_LanguageEnglish"] = "English",

        ["Loc_LanguageChinese"] = "简体中文",

        ["Loc_LanguageChineseTraditional"] = "繁體中文",

        ["Loc_LanguageKorean"] = "한국어",



        // Live Preview

        ["Loc_PreviewTitle"] = "实时预览",

        ["Loc_PreviewHint"] = "可通过点击、拖动或滚轮体验操作手感",

        ["Loc_PreviewBackdropLight"] = "背景: 浅色",

        ["Loc_PreviewBackdropDark"] = "背景: 深色",

        ["Loc_PreviewDetailTitle"] = "材质与光效放大预览",

        ["Loc_PreviewItemMasterVolume"] = "主音量",

        ["Loc_PreviewItemSpeakerOutput"] = "扬声器输出",

        ["Loc_PreviewItemBrightness"] = "屏幕亮度",

        ["Loc_PreviewItemDisplayBrightness"] = "显示器亮度",

        ["Loc_PreviewItemMicVolume"] = "麦克风音量",

        ["Loc_PreviewItemMicInput"] = "麦克风输入",

        ["Loc_PreviewItemAppVolume"] = "应用音量",

        ["Loc_PreviewItemBrowser"] = "浏览器",

        ["Loc_PreviewItemMusic"] = "音乐",



        // Tabs

        ["Loc_TabAppearance"] = "外观与材质",

        ["Loc_TabSliderMode"] = "滑块形状与手感",

        ["Loc_TabTriggers"] = "触发与显示项",



        // Tab 1: Appearance

        ["Loc_SurfaceAndLightingTitle"] = "表面材质与光照效果",

        ["Loc_TextureTitle"] = "材质",

        ["Loc_LightingTitle"] = "光照效果",

        ["Loc_TextureFlat"] = "纯色扁平",

        ["Loc_TextureAcrylic"] = "亚克力磨砂",

        ["Loc_TextureMesh"] = "细网纹",

        ["Loc_TextureNoise"] = "噪点颗粒",

        ["Loc_LightingMinimal"] = "简约",

        ["Loc_LightingSoftGlow"] = "柔和发光",

        ["Loc_LightingEdgeRimGlow"] = "边缘轮廓光",

        ["Loc_LightingPrismAurora"] = "极光棱镜",

        ["Loc_SliceStyleTitle"] = "扇区显示样式",

        ["Loc_SliceStyleIconOnly"] = "仅图标",

        ["Loc_SliceStyleIconAndText"] = "图标 + 名称",

        ["Loc_MenuScaleTitle"] = "菜单大小",

        ["Loc_ColorThemeTitle"] = "色彩主题与深浅模式",

        ["Loc_ThemeSystem"] = "跟随系统",

        ["Loc_ThemeLight"] = "浅色模式",

        ["Loc_ThemeDark"] = "深色模式",



        // Color Presets

        ["Loc_ColorMonochrome"] = "单色灰阶",

        ["Loc_ColorClearBlue"] = "清澈蓝",

        ["Loc_ColorTealRose"] = "青蓝与玫瑰",

        ["Loc_ColorSageGray"] = "鼠尾草灰",

        ["Loc_ColorCrimsonRose"] = "深红玫瑰",

        ["Loc_ColorFrostBlue"] = "冰霜蓝",

        ["Loc_ColorDustyPink"] = "灰粉色",

        ["Loc_ColorLavenderPurple"] = "薰衣草紫",

        ["Loc_ColorMintTeal"] = "薄荷青",

        ["Loc_ColorScarletRed"] = "猩红色",

        ["Loc_ColorSunsetOrange"] = "日落橙",

        ["Loc_ColorAmberGold"] = "琥珀金",

        ["Loc_ColorCitrusYellow"] = "柑橘黄",

        ["Loc_ColorLimeGreen"] = "青柠绿",

        ["Loc_ColorForestGreen"] = "森林绿",

        ["Loc_ColorEmeraldGreen"] = "祖母绿",

        ["Loc_ColorTurquoiseCyan"] = "绿松石青",

        ["Loc_ColorCobaltBlue"] = "钴蓝色",

        ["Loc_ColorDeepIndigo"] = "深靛蓝",

        ["Loc_ColorRoyalPurple"] = "皇家紫",

        ["Loc_ColorMagentaBerry"] = "品红莓果",



        // Tab 2: Slider Mode

        ["Loc_SliderStyleTitle"] = "环形滑块菜单操作模式",

        ["Loc_SliderMode1Title"] = "径向仪表型",

        ["Loc_SliderMode1Desc"] = "各扇区自身即是由内向外填充的仪表。通过光标距中心的距离或滚轮旋转直观调节 0〜100%。",

        ["Loc_SliderMode2Title"] = "圆周转盘型",

        ["Loc_SliderMode2Desc"] = "选中扇区后，沿中心圆周顺时针或逆时针拖动，如同旋转表盘般调节。",

        ["Loc_SliderMode3Title"] = "外周圆弧展开型",

        ["Loc_SliderMode3Desc"] = "选中扇区后在外周展开弧形滑轨，沿外周弧线滑动滑块进行精确调节。",

        ["Loc_SliderOptionTitle"] = "滑块手感选项",

        ["Loc_DirectHover"] = "拖动滑块释放鼠标按键时自动关闭悬浮菜单",

        ["Loc_WheelStepTitle"] = "鼠标滚轮单刻度步进量",



        // Tab 3: Triggers & Items

        ["Loc_TriggerTitle"] = "呼出触发设置",

        ["Loc_HotkeyLabel"] = "键盘快捷键",

        ["Loc_HotkeyRecording"] = "请按下按键... (Esc 取消)",

        ["Loc_HotkeyDisabled"] = "已禁用",
        ["Loc_PassThroughHotkey"] = "将快捷键传递给其他应用（不拦截/不覆盖现有快捷键）",
        ["Loc_PassThroughHotkeyDesc"] = "启用后，即使其他软件使用相同快捷键，VolumePie 也不会拦截按键，底层应用仍能正常接收快捷键。",

        ["Loc_MouseTriggerTitle"] = "鼠标按键触发",

        ["Loc_MouseTriggerNone"] = "无",

        ["Loc_MouseTriggerMiddleHold"] = "中键长按",

        ["Loc_MouseTriggerX1Hold"] = "侧键 1 长按",

        ["Loc_MouseTriggerX2Hold"] = "侧键 2 长按",

        ["Loc_MouseTriggerShiftRight"] = "Shift + 右键单击",

        ["Loc_MouseDelayTitle"] = "鼠标长按判定阈值",

        ["Loc_TriggerBehaviorTitle"] = "开闭触发行为",

        ["Loc_BehaviorSmart"] = "智能（短按保持常开 / 长按松开即关）",

        ["Loc_BehaviorHold"] = "按住（按住期间持续显示）",

        ["Loc_BehaviorToggle"] = "切换（每次按下切换开/关）",

        ["Loc_TargetItemsTitle"] = "菜单显示项与系统设置",

        ["Loc_ItemMasterVolume"] = "主音量",

        ["Loc_ItemBrightness"] = "屏幕亮度",

        ["Loc_ItemMicVolume"] = "麦克风输入音量",

        ["Loc_ItemAppSessions"] = "独立应用音量混音器",

        ["Loc_SoftwareDimmer"] = "在不支持硬件 DDC/CI 调光的显示器上使用软件调光蒙版",

        ["Loc_RunAtStartup"] = "Windows 登录时自动启动 VolumePie",



        // Footer

        ["Loc_BtnRevert"] = "恢复修改",

        ["Loc_BtnClose"] = "关闭",



        // Tray Menu & Overlays

        ["Loc_TrayOpenPie"] = "打开快捷菜单",

        ["Loc_TrayOutputDevice"] = "切换音频输出设备",
        ["Loc_TrayInputDevice"] = "切换音频输入设备",

        ["Loc_TraySettings"] = "打开设置...",

        ["Loc_TrayExit"] = "退出",

        ["Loc_SystemSounds"] = "系统声音",

        ["Loc_MoreApps"] = "更多应用",
        // Pie Overlay & Center Hub
        ["Loc_PieMasterVolume"] = "主音量",
        ["Loc_PieBrightness"] = "屏幕亮度",
        ["Loc_PieMicVolume"] = "麦克风音量",
        ["Loc_PieMicInput"] = "麦克风输入",
        ["Loc_PieSystemSounds"] = "系统提示音",
        ["Loc_PieWindowsSounds"] = "Windows 系统提示音",
        ["Loc_PiePlayingApp"] = "正在播放",
        ["Loc_PieAppMixer"] = "应用列表",
        ["Loc_PieAppsCountSuffix"] = "个应用 ▶",
        ["Loc_PieAppsListBadge"] = "列表 ▶",
        ["Loc_PieAppVolume"] = "应用音量",
        ["Loc_PieClickToOpenApps"] = "点击打开应用列表",
        ["Loc_PieBack"] = "◀ 返回",
        ["Loc_PieAppMixerTitle"] = "应用音量列表",
        ["Loc_PieClickToReturnMain"] = "点击返回主菜单",
        ["Loc_PieCloseHint"] = "中键点击关闭",
        ["Loc_PieBackHint"] = "右键点击返回",
        ["Loc_PieConfirmHint"] = "松开以确认",
        ["Loc_PieRadialAdjustHint"] = "拖拽 / 滚轮调节",
        ["Loc_PieRotaryAdjustHint"] = "圆周拖拽调节",
        ["Loc_PieArcAdjustHint"] = "拖拽外弧调节",
        ["Loc_PieOutputDeviceLabel"] = "输出:",
        ["Loc_PieOutputDeviceBarTooltip"] = "点击显示输出设备列表 / 滚轮即时切换",
        ["Loc_PieInputDeviceLabel"] = "输入:",
        ["Loc_PieInputDeviceBarTooltip"] = "点击显示输入设备列表 / 滚轮即时切换",
        ["Loc_PieDevicePopupTitle"] = "音频设备",
        ["Loc_PieDevicePopupOutputTab"] = "输出 (扬声器)",
        ["Loc_PieDevicePopupInputTab"] = "输入 (麦克风)",
        ["Loc_PieDevicePopupHint"] = "点击切换",
        ["Loc_PieNoOutputDevices"] = "未找到可用的输出设备",
        ["Loc_PieNoInputDevices"] = "未找到可用的输入设备",
        ["Loc_ModeRadial"] = "径向规",
        ["Loc_ModeRotary"] = "表盘",
        ["Loc_ModeArc"] = "外弧滑块",
    };




    private static readonly Dictionary<string, string> Ko = new()

    {

        // Window & Header

        ["Loc_WindowTitle"] = "VolumePie 커스터마이징 스튜디오",

        ["Loc_BtnTestOverlay"] = "실제 오버레이 표시",

        ["Loc_Language"] = "언어",

        ["Loc_LanguageTitle"] = "표시 언어",

        ["Loc_LanguageSubtitle"] = "UI 표시 언어를 변경합니다",

        ["Loc_LanguageSystem"] = "시스템",

        ["Loc_LanguageJapanese"] = "日本語",

        ["Loc_LanguageEnglish"] = "English",

        ["Loc_LanguageChinese"] = "简体中文",

        ["Loc_LanguageChineseTraditional"] = "繁體中文",

        ["Loc_LanguageKorean"] = "한국어",



        // Live Preview

        ["Loc_PreviewTitle"] = "라이브 미리보기",

        ["Loc_PreviewHint"] = "클릭, 드래그, 휠 스크롤로 조작감을 체험할 수 있습니다",

        ["Loc_PreviewBackdropLight"] = "배경: 라이트",

        ["Loc_PreviewBackdropDark"] = "배경: 다크",

        ["Loc_PreviewDetailTitle"] = "질감 및 조명 효과 확대 미리보기",

        ["Loc_PreviewItemMasterVolume"] = "마스터 볼륨",

        ["Loc_PreviewItemSpeakerOutput"] = "스피커 출력",

        ["Loc_PreviewItemBrightness"] = "화면 밝기",

        ["Loc_PreviewItemDisplayBrightness"] = "디스플레이 밝기",

        ["Loc_PreviewItemMicVolume"] = "마이크 볼륨",

        ["Loc_PreviewItemMicInput"] = "마이크 입력",

        ["Loc_PreviewItemAppVolume"] = "앱 볼륨",

        ["Loc_PreviewItemBrowser"] = "브라우저",

        ["Loc_PreviewItemMusic"] = "음악",



        // Tabs

        ["Loc_TabAppearance"] = "외관·질감·테마",

        ["Loc_TabSliderMode"] = "슬라이더 형태·조작감",

        ["Loc_TabTriggers"] = "트리거·표시 항목",



        // Tab 1: Appearance

        ["Loc_SurfaceAndLightingTitle"] = "표면 질감 및 조명 효과",

        ["Loc_TextureTitle"] = "질감",

        ["Loc_LightingTitle"] = "조명 효과",

        ["Loc_TextureFlat"] = "플랫",

        ["Loc_TextureAcrylic"] = "아크릴",

        ["Loc_TextureMesh"] = "메시",

        ["Loc_TextureNoise"] = "노이즈",

        ["Loc_LightingMinimal"] = "미니멀",

        ["Loc_LightingSoftGlow"] = "소프트 발광",

        ["Loc_LightingEdgeRimGlow"] = "엣지 발광",

        ["Loc_LightingPrismAurora"] = "오로라 반사",

        ["Loc_SliceStyleTitle"] = "슬라이스 표시 스타일",

        ["Loc_SliceStyleIconOnly"] = "아이콘만",

        ["Loc_SliceStyleIconAndText"] = "아이콘 + 이름",

        ["Loc_MenuScaleTitle"] = "메뉴 크기",

        ["Loc_ColorThemeTitle"] = "컬러 테마 및 명암 모드",

        ["Loc_ThemeSystem"] = "시스템",

        ["Loc_ThemeLight"] = "라이트 모드",

        ["Loc_ThemeDark"] = "다크 모드",



        // Color Presets

        ["Loc_ColorMonochrome"] = "모노크롬",

        ["Loc_ColorClearBlue"] = "클리어 블루",

        ["Loc_ColorTealRose"] = "틸 & 로즈",

        ["Loc_ColorSageGray"] = "세이지 그레이",

        ["Loc_ColorCrimsonRose"] = "크림슨 로즈",

        ["Loc_ColorFrostBlue"] = "프로스트 블루",

        ["Loc_ColorDustyPink"] = "더스티 핑크",

        ["Loc_ColorLavenderPurple"] = "라벤더 퍼플",

        ["Loc_ColorMintTeal"] = "민트 틸",

        ["Loc_ColorScarletRed"] = "스칼렛 레드",

        ["Loc_ColorSunsetOrange"] = "선셋 오렌지",

        ["Loc_ColorAmberGold"] = "앰버 골드",

        ["Loc_ColorCitrusYellow"] = "시트러스 옐로우",

        ["Loc_ColorLimeGreen"] = "라임 그린",

        ["Loc_ColorForestGreen"] = "포레스트 그린",

        ["Loc_ColorEmeraldGreen"] = "에메랄드 그린",

        ["Loc_ColorTurquoiseCyan"] = "터콰이즈 시안",

        ["Loc_ColorCobaltBlue"] = "코발트 블루",

        ["Loc_ColorDeepIndigo"] = "딥 인디고",

        ["Loc_ColorRoyalPurple"] = "로열 퍼플",

        ["Loc_ColorMagentaBerry"] = "마젠타 베리",



        // Tab 2: Slider Mode

        ["Loc_SliderStyleTitle"] = "슬라이더 파이 메뉴 조작 스타일",

        ["Loc_SliderMode1Title"] = "라디얼 게이지형",

        ["Loc_SliderMode1Desc"] = "각 부채꼴 자체가 안쪽에서 바깥쪽으로 채워지는 게이지가 됩니다. 중심과의 거리나 마우스 휠로 0~100%를 직관적으로 조절합니다.",

        ["Loc_SliderMode2Title"] = "원주 다이얼형",

        ["Loc_SliderMode2Desc"] = "부채꼴을 선택하고 원의 중심 주위를 시계/반시계 방향으로 문지르듯 드래그하여 다이얼 조절합니다.",

        ["Loc_SliderMode3Title"] = "외주 아크 전개형",

        ["Loc_SliderMode3Desc"] = "부채꼴을 선택하면 외곽에 원호 슬라이더가 펼쳐지며, 곡선을 따라 노브를 슬라이드하여 조절합니다.",

        ["Loc_SliderOptionTitle"] = "슬라이더 조작감 옵션",

        ["Loc_DirectHover"] = "슬라이더를 드래그한 후 마우스 버튼을 놓는 순간 파이 메뉴를 자동으로 닫기",

        ["Loc_WheelStepTitle"] = "마우스 휠 1눈금당 변화량",



        // Tab 3: Triggers & Items

        ["Loc_TriggerTitle"] = "호출 트리거 설정",

        ["Loc_HotkeyLabel"] = "키보드 단축키",

        ["Loc_HotkeyRecording"] = "키를 입력해 주세요... (Esc 취소)",

        ["Loc_HotkeyDisabled"] = "사용 안 함",
        ["Loc_PassThroughHotkey"] = "將快捷鍵傳遞給其他應用程式（不攔截/不覆蓋現有快捷鍵）",
        ["Loc_PassThroughHotkeyDesc"] = "啟用後，即使其他應用程式使用相同快捷鍵，VolumePie 也不會攔截按鍵，背景應用程式仍能正常接收快捷鍵。",

        ["Loc_MouseTriggerTitle"] = "마우스 버튼 트리거",

        ["Loc_MouseTriggerNone"] = "사용 안 함",

        ["Loc_MouseTriggerMiddleHold"] = "휠 클릭 길게 누르기",

        ["Loc_MouseTriggerX1Hold"] = "마우스 사이드 버튼 1 길게 누르기",

        ["Loc_MouseTriggerX2Hold"] = "마우스 사이드 버튼 2 길게 누르기",

        ["Loc_MouseTriggerShiftRight"] = "Shift + 우클릭",

        ["Loc_MouseDelayTitle"] = "마우스 길게 누르기 판정 시간",

        ["Loc_TriggerBehaviorTitle"] = "열기/닫기 동작 방식",

        ["Loc_BehaviorSmart"] = "스마트 (짧게 누름=열린 상태 유지 / 길게 누름=놓으면 닫힘)",

        ["Loc_BehaviorHold"] = "홀드 (누르고 있는 동안만 표시)",

        ["Loc_BehaviorToggle"] = "토글 (누를 때마다 열기 / 닫기)",

        ["Loc_TargetItemsTitle"] = "파이 메뉴 표시 항목 및 시스템 설정",

        ["Loc_ItemMasterVolume"] = "마스터 볼륨",

        ["Loc_ItemBrightness"] = "화면 밝기",

        ["Loc_ItemMicVolume"] = "마이크 입력 볼륨",

        ["Loc_ItemAppSessions"] = "앱별 볼륨 믹서",

        ["Loc_SoftwareDimmer"] = "하드웨어 밝기 제어를 지원하지 않는 모니터에서 소프트웨어 조광 오버레이 사용",

        ["Loc_RunAtStartup"] = "Windows 시작 시 VolumePie 자동 실행",



        // Footer

        ["Loc_BtnRevert"] = "변경 전으로 되돌리기",

        ["Loc_BtnClose"] = "닫기",



        // Tray Menu & Overlays

        ["Loc_TrayOpenPie"] = "파이 메뉴 열기",

        ["Loc_TrayOutputDevice"] = "출력 장치 전환",
        ["Loc_TrayInputDevice"] = "입력 장치 전환",

        ["Loc_TraySettings"] = "설정 열기...",

        ["Loc_TrayExit"] = "종료",

        ["Loc_SystemSounds"] = "시스템 소리",

        ["Loc_MoreApps"] = "기타 앱",
        // Pie Overlay & Center Hub
        ["Loc_PieMasterVolume"] = "마스터 볼륨",
        ["Loc_PieBrightness"] = "화면 밝기",
        ["Loc_PieMicVolume"] = "마이크 볼륨",
        ["Loc_PieMicInput"] = "마이크 입력",
        ["Loc_PieSystemSounds"] = "시스템 소리",
        ["Loc_PieWindowsSounds"] = "Windows 시스템 소리",
        ["Loc_PiePlayingApp"] = "재생 중 앱",
        ["Loc_PieAppMixer"] = "앱 목록",
        ["Loc_PieAppsCountSuffix"] = "개 앱 ▶",
        ["Loc_PieAppsListBadge"] = "목록 ▶",
        ["Loc_PieAppVolume"] = "앱 볼륨",
        ["Loc_PieClickToOpenApps"] = "클릭하여 앱 목록 열기",
        ["Loc_PieBack"] = "◀ 뒤로",
        ["Loc_PieAppMixerTitle"] = "앱 볼륨 목록",
        ["Loc_PieClickToReturnMain"] = "클릭하여 기본으로",
        ["Loc_PieCloseHint"] = "가운데 클릭으로 닫기",
        ["Loc_PieBackHint"] = "우클릭으로 뒤로",
        ["Loc_PieConfirmHint"] = "놓아서 확인",
        ["Loc_PieRadialAdjustHint"] = "드래그 / 휠로 조절",
        ["Loc_PieRotaryAdjustHint"] = "회전 드래그로 조절",
        ["Loc_PieArcAdjustHint"] = "외부 아크 드래그",
        ["Loc_PieOutputDeviceLabel"] = "출력:",
        ["Loc_PieOutputDeviceBarTooltip"] = "클릭하여 출력 장치 목록 표시 / 휠 회전으로 즉시 전환",
        ["Loc_PieInputDeviceLabel"] = "입력:",
        ["Loc_PieInputDeviceBarTooltip"] = "클릭하여 입력 장치 목록 표시 / 휠 회전으로 즉시 전환",
        ["Loc_PieDevicePopupTitle"] = "오디오 장치",
        ["Loc_PieDevicePopupOutputTab"] = "출력 (스피커)",
        ["Loc_PieDevicePopupInputTab"] = "입력 (마이크)",
        ["Loc_PieDevicePopupHint"] = "클릭하여 전환",
        ["Loc_PieNoOutputDevices"] = "사용 가능한 출력 장치가 없습니다",
        ["Loc_PieNoInputDevices"] = "사용 가능한 입력 장치가 없습니다",
        ["Loc_ModeRadial"] = "방사형",
        ["Loc_ModeRotary"] = "다이얼",
        ["Loc_ModeArc"] = "외부 아크",
    };




    private static readonly Dictionary<string, string> ZhHant = new()

    {

        // Window & Header

        ["Loc_WindowTitle"] = "VolumePie 自訂工作室",

        ["Loc_BtnTestOverlay"] = "顯示實際覆蓋層",

        ["Loc_Language"] = "語言",

        ["Loc_LanguageTitle"] = "顯示語言",

        ["Loc_LanguageSubtitle"] = "切換 UI 顯示語言",

        ["Loc_LanguageSystem"] = "系統",

        ["Loc_LanguageJapanese"] = "日本語",

        ["Loc_LanguageEnglish"] = "English",

        ["Loc_LanguageChinese"] = "简体中文",

        ["Loc_LanguageChineseTraditional"] = "繁體中文",

        ["Loc_LanguageKorean"] = "한국어",



        // Live Preview

        ["Loc_PreviewTitle"] = "即時預覽",

        ["Loc_PreviewHint"] = "可透過 點擊 / 拖曳 / 滾輪 體驗操作手感",

        ["Loc_PreviewBackdropLight"] = "背景: 亮色",

        ["Loc_PreviewBackdropDark"] = "背景: 深色",

        ["Loc_PreviewDetailTitle"] = "質感與光效放大顯示",

        ["Loc_PreviewItemMasterVolume"] = "主音量",

        ["Loc_PreviewItemSpeakerOutput"] = "喇叭輸出",

        ["Loc_PreviewItemBrightness"] = "螢幕亮度",

        ["Loc_PreviewItemDisplayBrightness"] = "顯示器亮度",

        ["Loc_PreviewItemMicVolume"] = "麥克風音量",

        ["Loc_PreviewItemMicInput"] = "麥克風輸入",

        ["Loc_PreviewItemAppVolume"] = "應用程式音量",

        ["Loc_PreviewItemBrowser"] = "瀏覽器",

        ["Loc_PreviewItemMusic"] = "音樂",



        // Tabs

        ["Loc_TabAppearance"] = "外觀・材質・主題",

        ["Loc_TabSliderMode"] = "滑桿形狀・操作手感",

        ["Loc_TabTriggers"] = "觸發方式・顯示項目",



        // Tab 1: Appearance

        ["Loc_SurfaceAndLightingTitle"] = "表面材質與光效",

        ["Loc_TextureTitle"] = "材質紋理",

        ["Loc_LightingTitle"] = "光效",

        ["Loc_TextureFlat"] = "平坦",

        ["Loc_TextureAcrylic"] = "壓克力 (Acrylic)",

        ["Loc_TextureMesh"] = "網格",

        ["Loc_TextureNoise"] = "噪點",

        ["Loc_LightingMinimal"] = "極簡",

        ["Loc_LightingSoftGlow"] = "柔和微光",

        ["Loc_LightingEdgeRimGlow"] = "邊緣發光",

        ["Loc_LightingPrismAurora"] = "極光反射",

        ["Loc_SliceStyleTitle"] = "扇形項目顯示樣式",

        ["Loc_SliceStyleIconOnly"] = "僅圖示",

        ["Loc_SliceStyleIconAndText"] = "圖示 ＋ 名稱",

        ["Loc_MenuScaleTitle"] = "選單大小",

        ["Loc_ColorThemeTitle"] = "色彩主題與深淺模式",

        ["Loc_ThemeSystem"] = "系統",

        ["Loc_ThemeLight"] = "淺色模式",

        ["Loc_ThemeDark"] = "深色模式",



        // Color Presets

        ["Loc_ColorMonochrome"] = "黑白單色",

        ["Loc_ColorClearBlue"] = "澄澈藍",

        ["Loc_ColorTealRose"] = "青藍與玫瑰",

        ["Loc_ColorSageGray"] = "鼠尾草灰",

        ["Loc_ColorCrimsonRose"] = "緋紅玫瑰",

        ["Loc_ColorFrostBlue"] = "霜雪藍",

        ["Loc_ColorDustyPink"] = "灰粉紅",

        ["Loc_ColorLavenderPurple"] = "薰衣草紫",

        ["Loc_ColorMintTeal"] = "薄荷青",

        ["Loc_ColorScarletRed"] = "猩紅",

        ["Loc_ColorSunsetOrange"] = "夕陽橘",

        ["Loc_ColorAmberGold"] = "琥珀金",

        ["Loc_ColorCitrusYellow"] = "柑橘黃",

        ["Loc_ColorLimeGreen"] = "青檸綠",

        ["Loc_ColorForestGreen"] = "森林綠",

        ["Loc_ColorEmeraldGreen"] = "祖母綠",

        ["Loc_ColorTurquoiseCyan"] = "綠松青",

        ["Loc_ColorCobaltBlue"] = "鈷藍",

        ["Loc_ColorDeepIndigo"] = "深靛藍",

        ["Loc_ColorRoyalPurple"] = "皇家紫",

        ["Loc_ColorMagentaBerry"] = "洋紅果莓",



        // Tab 2: Slider Mode

        ["Loc_SliderStyleTitle"] = "圓盤滑桿操作樣式",

        ["Loc_SliderMode1Title"] = "放射型儀表",

        ["Loc_SliderMode1Desc"] = "滑鼠從圓心向外移動的距離即為 0〜100% 的音量大小。最直覺且反應迅速。",

        ["Loc_SliderMode2Title"] = "旋轉式旋鈕",

        ["Loc_SliderMode2Desc"] = "沿著圓周方向（順時針 / 逆時針）拖曳旋轉以調節音量。適合精細調整。",

        ["Loc_SliderMode3Title"] = "外環展開弧形",

        ["Loc_SliderMode3Desc"] = "選中扇區時外圈展開弧形滑桿，順著軌道直覺調節音量。",

        ["Loc_SliderOptionTitle"] = "滑桿進階選項",

        ["Loc_DirectHover"] = "懸停扇區時立即直接調節（無需長按扇區）",

        ["Loc_WheelStepTitle"] = "滑鼠滾輪調節步進量",



        // Tab 3: Triggers & Items

        ["Loc_TriggerTitle"] = "熱鍵與滑鼠觸發方式",

        ["Loc_HotkeyLabel"] = "呼叫熱鍵",

        ["Loc_HotkeyRecording"] = "請按下任意按鍵組合...",

        ["Loc_HotkeyDisabled"] = "（已停用）",
        ["Loc_PassThroughHotkey"] = "단축키를 다른 앱에도 전달 (단축키를 차단/덮어쓰지 않음)",
        ["Loc_PassThroughHotkeyDesc"] = "활성화하면 기존 소프트웨어가 동일한 단축키를 사용하더라도 키 입력을 가로채지 않고 백그라운드 앱에도 키를 전달합니다.",

        ["Loc_MouseTriggerTitle"] = "滑鼠呼叫手勢",

        ["Loc_MouseTriggerNone"] = "不使用",

        ["Loc_MouseTriggerMiddleHold"] = "滾輪長按",

        ["Loc_MouseTriggerX1Hold"] = "滑鼠側鍵 1 長按",

        ["Loc_MouseTriggerX2Hold"] = "滑鼠側鍵 2 長按",

        ["Loc_MouseTriggerShiftRight"] = "Shift + 右鍵點擊",

        ["Loc_MouseDelayTitle"] = "滑鼠長按判定時間",

        ["Loc_TriggerBehaviorTitle"] = "開啟 / 關閉行為模式",

        ["Loc_BehaviorSmart"] = "智慧模式（短按＝保持開啟 / 長按＝放開即關閉）",

        ["Loc_BehaviorHold"] = "按住模式（僅在按住時顯示）",

        ["Loc_BehaviorToggle"] = "切換模式（每次按下開啟 / 關閉）",

        ["Loc_TargetItemsTitle"] = "圓盤選單顯示項目與系統設定",

        ["Loc_ItemMasterVolume"] = "主音量",

        ["Loc_ItemBrightness"] = "螢幕亮度",

        ["Loc_ItemMicVolume"] = "麥克風輸入音量",

        ["Loc_ItemAppSessions"] = "個別應用程式音量混音器",

        ["Loc_SoftwareDimmer"] = "在不支援硬體亮度調節的螢幕上使用軟體遮罩調光",

        ["Loc_RunAtStartup"] = "開機時自動啟動 VolumePie",



        // Footer

        ["Loc_BtnRevert"] = "還原所有變更",

        ["Loc_BtnClose"] = "關閉",



        // Tray Menu & Overlays

        ["Loc_TrayOpenPie"] = "開啟圓盤選單",

        ["Loc_TrayOutputDevice"] = "切換輸出裝置",
        ["Loc_TrayInputDevice"] = "切換輸入裝置",

        ["Loc_TraySettings"] = "開啟設定...",

        ["Loc_TrayExit"] = "結束",

        ["Loc_SystemSounds"] = "系統音效",

        ["Loc_MoreApps"] = "更多應用程式",
        // Pie Overlay & Center Hub
        ["Loc_PieMasterVolume"] = "主要音量",
        ["Loc_PieBrightness"] = "螢幕亮度",
        ["Loc_PieMicVolume"] = "麥克風音量",
        ["Loc_PieMicInput"] = "麥克風輸入",
        ["Loc_PieSystemSounds"] = "系統音效",
        ["Loc_PieWindowsSounds"] = "Windows 系統音效",
        ["Loc_PiePlayingApp"] = "正在播放",
        ["Loc_PieAppMixer"] = "應用程式清單",
        ["Loc_PieAppsCountSuffix"] = "個應用程式 ▶",
        ["Loc_PieAppsListBadge"] = "清單 ▶",
        ["Loc_PieAppVolume"] = "應用程式音量",
        ["Loc_PieClickToOpenApps"] = "點擊開啟應用程式清單",
        ["Loc_PieBack"] = "◀ 返回",
        ["Loc_PieAppMixerTitle"] = "應用程式音量清單",
        ["Loc_PieClickToReturnMain"] = "點擊返回主選單",
        ["Loc_PieCloseHint"] = "中鍵點擊關閉",
        ["Loc_PieBackHint"] = "右鍵點擊返回",
        ["Loc_PieConfirmHint"] = "放開以確認",
        ["Loc_PieRadialAdjustHint"] = "拖曳 / 滾輪調節",
        ["Loc_PieRotaryAdjustHint"] = "圓周拖曳調節",
        ["Loc_PieArcAdjustHint"] = "拖曳外弧調節",
        ["Loc_PieOutputDeviceLabel"] = "輸出:",
        ["Loc_PieOutputDeviceBarTooltip"] = "點擊顯示輸出裝置清單 / 滾輪即時切換",
        ["Loc_PieInputDeviceLabel"] = "輸入:",
        ["Loc_PieInputDeviceBarTooltip"] = "點擊顯示輸入裝置清單 / 滾輪即時切換",
        ["Loc_PieDevicePopupTitle"] = "音訊裝置",
        ["Loc_PieDevicePopupOutputTab"] = "輸出 (喇叭)",
        ["Loc_PieDevicePopupInputTab"] = "輸入 (麥克風)",
        ["Loc_PieDevicePopupHint"] = "點擊切換",
        ["Loc_PieNoOutputDevices"] = "找不到可用的輸出裝置",
        ["Loc_PieNoInputDevices"] = "找不到可用的輸入裝置",
        ["Loc_ModeRadial"] = "放射型",
        ["Loc_ModeRotary"] = "轉盤",
        ["Loc_ModeArc"] = "外弧滑桿",
    };




    private static Dictionary<string, string> _activeDict = Ja;



    public static void ApplyLanguage(AppLanguage setting)

    {

        _currentSetting = setting;

        if (setting == AppLanguage.Japanese)

        {

            _activeDict = Ja;

            _isJapanese = true;

        }

        else if (setting == AppLanguage.English)

        {

            _activeDict = En;

            _isJapanese = false;

        }

        else if (setting == AppLanguage.Chinese)

        {

            _activeDict = Zh;

            _isJapanese = false;

        }

        else if (setting == AppLanguage.ChineseTraditional)

        {

            _activeDict = ZhHant;

            _isJapanese = false;

        }

        else if (setting == AppLanguage.Korean)

        {

            _activeDict = Ko;

            _isJapanese = false;

        }

        else // System default

        {

            var uiLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

            if (uiLang == "ja")

            {

                _activeDict = Ja;

                _isJapanese = true;

            }

            else if (uiLang == "zh")

            {

                var name = CultureInfo.CurrentUICulture.Name.ToLowerInvariant();

                if (name.Contains("tw") || name.Contains("hk") || name.Contains("mo") || name.Contains("hant"))

                {

                    _activeDict = ZhHant;

                }

                else

                {

                    _activeDict = Zh;

                }

                _isJapanese = false;

            }

            else if (uiLang == "ko")

            {

                _activeDict = Ko;

                _isJapanese = false;

            }

            else

            {

                _activeDict = En;

                _isJapanese = false;

            }

        }



        // Populate Application resources so {DynamicResource Loc_...} bindings update

        var app = System.Windows.Application.Current;

        if (app != null)

        {

            foreach (var kv in _activeDict)

            {

                app.Resources[kv.Key] = kv.Value;

            }

        }



        LanguageChanged?.Invoke();

    }



    public static string Get(string key)

    {

        if (_activeDict.TryGetValue(key, out var val))

            return val;

        if (Ja.TryGetValue(key, out var fallback))

            return fallback;

        return key;

    }

}

