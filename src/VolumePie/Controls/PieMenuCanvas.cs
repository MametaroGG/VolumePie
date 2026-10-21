using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VolumePie.Models;
using VolumePie.Services;

namespace VolumePie.Controls;

public class PieMenuCanvas : FrameworkElement
{
    private readonly List<PieItem> _items = new();
    private PieSliderMode _sliderMode = PieSliderMode.RadialGauge;
    private ThemePalette _palette = ThemePalette.Dark;
    private UiSurfaceTexture _surfaceTexture = UiSurfaceTexture.Flat;
    private UiLightingEffect _lightingEffect = UiLightingEffect.Minimal;
    private SliceLabelStyle _sliceLabelStyle = SliceLabelStyle.IconOnly;
    private System.Windows.Media.Brush? _backdropAcrylicBrush;
    private bool _closeOnDragRelease;
    private bool _isTriggerHeld;
    private int _wheelStep = 3;

    private int _hoveredIndex = -1;
    private int _lockedIndex = -1;
    private int _previewFallbackIndex = -1;
    private bool _isDraggingSlider;
    private bool _isHoveringMuteButton;
    private bool _isHoveringCenterHub;

    private System.Windows.Point _cursorPos;
    private System.Windows.Point _initialOpenCursorPos;
    private bool _hasInitialOpenCursorPos;
    private double _cursorAngleDeg;
    private double _cursorRadius;
    private double _lastRotaryAngleDeg;

    // Radii constants (Canvas is 540x540, center at 270,270)
    private const double CenterR = 72.0;
    private const double InnerR = 78.0;
    private const double MuteBadgeRadius = 105.0;

    // Mode 1 & 2 Outer Radius
    private const double StandardOuterR = 226.0;
    private const double GaugeStartR = InnerR;
    private const double GaugeEndR = StandardOuterR;

    // Mode 3 Radii (Compact inner slice + Expanded outer arc slider)
    private const double Mode3SliceOuterR = 174.0;
    private const double Mode3ArcInnerR = 186.0;
    private const double Mode3ArcOuterR = 240.0;
    private const double Mode3ArcCenterR = 213.0;
    private const double Mode3ArcSpanDeg = 104.0; // Total angular span of the expanded outer arc

    private static readonly Typeface MediumTypeface = new(new System.Windows.Media.FontFamily("Yu Gothic UI, Segoe UI, Meiryo UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
    private static readonly Typeface UiTypeface = new(new System.Windows.Media.FontFamily("Yu Gothic UI, Segoe UI, Meiryo UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface BoldTypeface = new(new System.Windows.Media.FontFamily("Yu Gothic UI, Segoe UI, Meiryo UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public event Action? RequestClose;
    public event Action? RequestGoBack;
    public event Action<PieItem>? ItemValueAdjusted;
    public bool IsInSubPage { get; set; }

    public PieMenuCanvas()
    {
        Width = 540;
        Height = 540;
        Focusable = true;
        FocusVisualStyle = null;
        Cursor = System.Windows.Input.Cursors.Hand;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
    }

    public PieSliderMode SliderMode
    {
        get => _sliderMode;
        set
        {
            _sliderMode = value;
            _isDraggingSlider = false;
            InvalidateVisual();
        }
    }

    public ThemePalette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            InvalidateVisual();
        }
    }

    public UiSurfaceTexture SurfaceTexture
    {
        get => _surfaceTexture;
        set
        {
            _surfaceTexture = value;
            InvalidateVisual();
        }
    }

    public System.Windows.Media.Brush? BackdropAcrylicBrush
    {
        get => _backdropAcrylicBrush;
        set
        {
            _backdropAcrylicBrush = value;
            InvalidateVisual();
        }
    }

    public UiLightingEffect LightingEffect
    {
        get => _lightingEffect;
        set
        {
            _lightingEffect = value;
            InvalidateVisual();
        }
    }

    public SliceLabelStyle SliceLabelStyle
    {
        get => _sliceLabelStyle;
        set
        {
            _sliceLabelStyle = value;
            InvalidateVisual();
        }
    }

    public bool DirectHoverAdjust
    {
        get => _closeOnDragRelease;
        set
        {
            _closeOnDragRelease = value;
            InvalidateVisual();
        }
    }

    public bool IsTriggerHeld
    {
        get => _isTriggerHeld;
        set
        {
            bool wasHeld = _isTriggerHeld;
            _isTriggerHeld = value;
            if (!value && wasHeld)
            {
                bool wasDragging = _isDraggingSlider;
                CancelDragAndReleaseCapture();
                if (wasDragging && _closeOnDragRelease)
                {
                    RequestClose?.Invoke();
                }
            }
            InvalidateVisual();
        }
    }

    public int WheelStep
    {
        get => _wheelStep;
        set => _wheelStep = Math.Clamp(value, 1, 20);
    }

    public int PreviewFallbackIndex
    {
        get => _previewFallbackIndex;
        set
        {
            _previewFallbackIndex = value;
            InvalidateVisual();
        }
    }

    public void SetItems(IEnumerable<PieItem> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _hoveredIndex = -1;
        _lockedIndex = -1;
        _isDraggingSlider = false;
        InvalidateVisual();
    }

    public void ResetInteractionState()
    {
        CancelDragAndReleaseCapture();
        _hoveredIndex = -1;
        _lockedIndex = -1;
        _isHoveringMuteButton = false;
        _isHoveringCenterHub = true;
        _hasInitialOpenCursorPos = false;
        InvalidateVisual();
    }

    public void CancelDragAndReleaseCapture()
    {
        _isDraggingSlider = false;
        _lockedIndex = -1;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    public bool IsInteractingWithSlice => _isDraggingSlider || (_hoveredIndex >= 0 && !_isHoveringCenterHub);

    private static bool IsAnyDragButtonPressed(System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed ||
            e.MiddleButton == MouseButtonState.Pressed ||
            e.XButton1 == MouseButtonState.Pressed ||
            e.XButton2 == MouseButtonState.Pressed)
        {
            return true;
        }

        // Also check physical Win32 key state in case the mouse trigger button was pressed BEFORE the overlay window opened
        return (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0 ||
               (GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0 ||
               (GetAsyncKeyState(VK_XBUTTON1) & 0x8000) != 0 ||
               (GetAsyncKeyState(VK_XBUTTON2) & 0x8000) != 0;
    }

    private const int VK_LBUTTON = 0x01;
    private const int VK_MBUTTON = 0x04;
    private const int VK_XBUTTON1 = 0x05;
    private const int VK_XBUTTON2 = 0x06;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var center = new System.Windows.Point(ActualWidth / 2.0, ActualHeight / 2.0);
        _cursorPos = e.GetPosition(this);

        if (!_hasInitialOpenCursorPos)
        {
            _initialOpenCursorPos = _cursorPos;
            _hasInitialOpenCursorPos = true;
        }

        double dx = _cursorPos.X - center.X;
        double dy = _cursorPos.Y - center.Y;
        _cursorRadius = Math.Sqrt(dx * dx + dy * dy);
        _cursorAngleDeg = NormalizeAngleDeg(Math.Atan2(dy, dx) * 180.0 / Math.PI);

        if (_items.Count == 0)
        {
            InvalidateVisual();
            return;
        }

        bool anyButtonPressed = IsAnyDragButtonPressed(e);

        // Safety check: if no mouse button is physically pressed, never remain in dragging state!
        // This guarantees that bare mouse hover NEVER changes the value.
        if (!anyButtonPressed)
        {
            if (_isDraggingSlider || IsMouseCaptured)
            {
                CancelDragAndReleaseCapture();
            }
            _isTriggerHeld = false;
        }

        // If actively dragging with a mouse button held down, update the locked slice
        if (_isDraggingSlider && anyButtonPressed && _lockedIndex >= 0 && _lockedIndex < _items.Count)
        {
            ApplySliderAdjustment(_lockedIndex);
            InvalidateVisual();
            return;
        }

        _isHoveringCenterHub = _cursorRadius < CenterR;

        if (_isHoveringCenterHub)
        {
            _hoveredIndex = -1;
            _lockedIndex = -1;
            _isHoveringMuteButton = false;
            InvalidateVisual();
            return;
        }

        // In Mode 3 (OuterArcExpand), if a slice is already hovered and the cursor moves out into the outer arc band,
        // keep that slice active while the cursor is within the expanded outer arc!
        if (_sliderMode == PieSliderMode.OuterArcExpand && _hoveredIndex >= 0 && _cursorRadius >= Mode3SliceOuterR - 4.0 && _cursorRadius <= Mode3ArcOuterR + 24.0)
        {
            double midAngle = GetSliceMidAngle(_hoveredIndex, _items.Count);
            double delta = Math.Abs(ShortestAngleDiffDeg(midAngle, _cursorAngleDeg));
            if (delta <= (Mode3ArcSpanDeg / 2.0 + 14.0))
            {
                _isHoveringMuteButton = false;
                if (anyButtonPressed && _cursorRadius >= Mode3ArcInnerR - 8.0)
                {
                    _lockedIndex = _hoveredIndex;
                    _isDraggingSlider = true;
                    ApplyOuterArcAdjustment(_lockedIndex);
                }
                InvalidateVisual();
                return;
            }
        }

        double maxHitR = _sliderMode == PieSliderMode.OuterArcExpand ? Mode3ArcOuterR + 12.0 : StandardOuterR + 20.0;
        if (_cursorRadius <= maxHitR)
        {
            int newIndex = GetSliceIndexFromAngleWithHysteresis(_cursorAngleDeg, _items.Count, _hoveredIndex);
            if (newIndex != _hoveredIndex)
            {
                _hoveredIndex = newIndex;
                _lastRotaryAngleDeg = _cursorAngleDeg;
            }

            _isHoveringMuteButton = CheckMuteBadgeHit(_hoveredIndex, center, _cursorPos);

            // If user is physically holding a mouse button (Left / Middle / XButton1 / XButton2)
            // and drags out into a slice, latch and adjust the slider while the button remains held!
            if (anyButtonPressed && _hoveredIndex >= 0 && !_isHoveringMuteButton && _items[_hoveredIndex].Category != PieItemCategory.Navigation)
            {
                double moveDx = _cursorPos.X - _initialOpenCursorPos.X;
                double moveDy = _cursorPos.Y - _initialOpenCursorPos.Y;
                bool movedFromOpenPoint = (moveDx * moveDx + moveDy * moveDy) >= 36.0; // >= 6px movement

                if (movedFromOpenPoint)
                {
                    if (_sliderMode == PieSliderMode.RadialGauge && _cursorRadius >= GaugeStartR)
                    {
                        _lockedIndex = _hoveredIndex;
                        _isDraggingSlider = true;
                        ApplyRadialAdjustment(_lockedIndex);
                    }
                    else if (_sliderMode == PieSliderMode.RotaryDial && _cursorRadius >= InnerR + 16)
                    {
                        _lockedIndex = _hoveredIndex;
                        _isDraggingSlider = true;
                        _lastRotaryAngleDeg = _cursorAngleDeg;
                    }
                }
            }
        }
        else
        {
            _hoveredIndex = -1;
            _lockedIndex = -1;
            _isHoveringMuteButton = false;
        }

        InvalidateVisual();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Right or MouseButton.Middle or MouseButton.XButton1 or MouseButton.XButton2))
            return;

        var center = new System.Windows.Point(ActualWidth / 2.0, ActualHeight / 2.0);
        _cursorPos = e.GetPosition(this);
        double dx = _cursorPos.X - center.X;
        double dy = _cursorPos.Y - center.Y;
        _cursorRadius = Math.Sqrt(dx * dx + dy * dy);
        _cursorAngleDeg = NormalizeAngleDeg(Math.Atan2(dy, dx) * 180.0 / Math.PI);

        if (e.ChangedButton == MouseButton.Right)
        {
            CancelDragAndReleaseCapture();
            if (IsInSubPage)
            {
                RequestGoBack?.Invoke();
            }
            else
            {
                RequestClose?.Invoke();
            }
            e.Handled = true;
            return;
        }

        if (_cursorRadius < CenterR)
        {
            CancelDragAndReleaseCapture();
            if (IsInSubPage)
            {
                RequestGoBack?.Invoke();
            }
            else
            {
                RequestClose?.Invoke();
            }
            e.Handled = true;
            return;
        }

        double maxHitR = _sliderMode == PieSliderMode.OuterArcExpand ? Mode3ArcOuterR + 14.0 : StandardOuterR + 16.0;
        if (_cursorRadius > maxHitR)
        {
            CancelDragAndReleaseCapture();
            RequestClose?.Invoke();
            e.Handled = true;
            return;
        }

        if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count)
        {
            var item = _items[_hoveredIndex];
            if (item.Category == PieItemCategory.Navigation)
            {
                CancelDragAndReleaseCapture();
                item.OnInvoked?.Invoke();
                e.Handled = true;
                return;
            }

            // Check if clicking the mute badge
            if (CheckMuteBadgeHit(_hoveredIndex, center, _cursorPos))
            {
                item.ToggleMute();
                ItemValueAdjusted?.Invoke(item);
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // Start slider drag (works with Left-Click OR Middle/Side mouse button!)
            _lockedIndex = _hoveredIndex;
            _isDraggingSlider = true;
            _lastRotaryAngleDeg = _cursorAngleDeg;
            CaptureMouse();

            // In Mode 1 (RadialGauge) or Mode 3 (OuterArcExpand when clicking on outer arc), immediately apply position
            if (_sliderMode == PieSliderMode.RadialGauge)
            {
                ApplyRadialAdjustment(_lockedIndex);
            }
            else if (_sliderMode == PieSliderMode.OuterArcExpand && _cursorRadius >= Mode3ArcInnerR - 10.0)
            {
                ApplyOuterArcAdjustment(_lockedIndex);
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        CancelDragAndReleaseCapture();
        RequestClose?.Invoke();
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Middle or MouseButton.XButton1 or MouseButton.XButton2))
            return;

        if (!IsAnyDragButtonPressed(e))
        {
            bool wasDragging = _isDraggingSlider;
            if (_isDraggingSlider || IsMouseCaptured)
            {
                CancelDragAndReleaseCapture();
                InvalidateVisual();
                e.Handled = true;

                if (wasDragging && _closeOnDragRelease)
                {
                    RequestClose?.Invoke();
                }
            }
        }
    }

    protected override void OnLostMouseCapture(System.Windows.Input.MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _isDraggingSlider = false;
        _lockedIndex = -1;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_isDraggingSlider)
        {
            _hoveredIndex = -1;
            _isHoveringMuteButton = false;
            InvalidateVisual();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        int targetIdx = _isDraggingSlider ? _lockedIndex : _hoveredIndex;
        if (targetIdx >= 0 && targetIdx < _items.Count)
        {
            var item = _items[targetIdx];
            if (item.Category == PieItemCategory.Navigation)
            {
                e.Handled = true;
                return;
            }

            bool fineStep = (Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != 0;
            double step = fineStep ? 1.0 : _wheelStep;
            double delta = (e.Delta > 0 ? 1 : -1) * step;
            item.SetLevel(Math.Round(item.Value + delta));
            ItemValueAdjusted?.Invoke(item);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private void ApplySliderAdjustment(int index)
    {
        switch (_sliderMode)
        {
            case PieSliderMode.RadialGauge:
                ApplyRadialAdjustment(index);
                break;
            case PieSliderMode.RotaryDial:
                ApplyRotaryAdjustment(index);
                break;
            case PieSliderMode.OuterArcExpand:
                ApplyOuterArcAdjustment(index);
                break;
        }
    }

    private void ApplyRadialAdjustment(int index)
    {
        var item = _items[index];
        double ratio = (_cursorRadius - GaugeStartR) / (GaugeEndR - GaugeStartR);
        double pct = Math.Round(Math.Clamp(ratio * 100.0, 0.0, 100.0));
        item.SetLevel(pct);
        ItemValueAdjusted?.Invoke(item);
    }

    private void ApplyRotaryAdjustment(int index)
    {
        var item = _items[index];
        double diffDeg = ShortestAngleDiffDeg(_lastRotaryAngleDeg, _cursorAngleDeg);
        if (Math.Abs(diffDeg) > 0.15)
        {
            // 220 degrees of clockwise rotation = +100%
            double deltaPct = diffDeg / 2.2;
            item.SetLevel(Math.Round(Math.Clamp(item.Value + deltaPct, 0.0, 100.0), 1));
            _lastRotaryAngleDeg = _cursorAngleDeg;
            ItemValueAdjusted?.Invoke(item);
        }
    }

    private void ApplyOuterArcAdjustment(int index)
    {
        var item = _items[index];
        double midAngle = GetSliceMidAngle(index, _items.Count);
        double usableSpan = Mode3ArcSpanDeg - 12.0; // Padding for end caps
        double relAngle = ShortestAngleDiffDeg(midAngle, _cursorAngleDeg);
        double ratio = (relAngle + usableSpan / 2.0) / usableSpan;
        double pct = Math.Round(Math.Clamp(ratio * 100.0, 0.0, 100.0));
        item.SetLevel(pct);
        ItemValueAdjusted?.Invoke(item);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var center = new System.Windows.Point(ActualWidth / 2.0, ActualHeight / 2.0);
        int count = _items.Count;
        if (count == 0) return;

        int activeIdx = _isDraggingSlider ? _lockedIndex : _hoveredIndex;
        if (activeIdx < 0 && _previewFallbackIndex >= 0 && _previewFallbackIndex < count)
        {
            activeIdx = _previewFallbackIndex;
        }

        // 1. Natural multi-stop Gaussian drop-shadow with subtle vertical elevation offset
        double platterR = _sliderMode == PieSliderMode.OuterArcExpand ? Mode3SliceOuterR + 6.0 : StandardOuterR + 7.0;
        DrawNaturalPlatterShadow(dc, center, platterR);

        // 2. Unified Circular Base Platter inside the pie ring
        var platterGeo = new EllipseGeometry(center, platterR, platterR);
        if (_surfaceTexture == UiSurfaceTexture.Acrylic)
        {
            var acrylicBackdrop = _backdropAcrylicBrush ?? ThemeService.GetSimulatedAcrylicBackdropBrush(_palette);
            dc.DrawGeometry(acrylicBackdrop, null, platterGeo);
            dc.DrawGeometry(ThemeService.GetAcrylicGlassSheenBrush(_palette.IsLight, false), null, platterGeo);
            dc.DrawGeometry(null, new System.Windows.Media.Pen(ThemeService.GetAcrylicSpecularBorderBrush(false), 1.4), platterGeo);
        }
        else
        {
            var platterBorderPen = new System.Windows.Media.Pen(new SolidColorBrush(_palette.PlatterBorder), 1.5);
            dc.DrawGeometry(new SolidColorBrush(_palette.PlatterBg), platterBorderPen, platterGeo);
        }

        double sliceStep = 360.0 / count;
        double gapDeg = count > 1 ? 3.4 : 0.0;
        // Lateral sideways expansion angle when focused (widens slice laterally)
        double lateralExpandDeg = count > 1 ? Math.Clamp(36.0 / count, 4.0, 9.0) : 0.0;

        // 1st Pass: Draw all non-selected slices
        for (int i = 0; i < count; i++)
        {
            if (i == activeIdx) continue;
            var item = _items[i];
            double midAngle = GetSliceMidAngle(i, count);
            double startAngle = midAngle - (sliceStep - gapDeg) / 2.0;
            double endAngle = midAngle + (sliceStep - gapDeg) / 2.0;

            DrawSliceByMode(dc, center, item, i, isSelected: false, startAngle, endAngle, midAngle);
        }

        // 2nd Pass: Draw the focused/selected slice ON TOP with sideways expansion and drop shadow
        if (activeIdx >= 0 && activeIdx < count)
        {
            var item = _items[activeIdx];
            double midAngle = GetSliceMidAngle(activeIdx, count);
            double startAngle = midAngle - (sliceStep - gapDeg + lateralExpandDeg) / 2.0;
            double endAngle = midAngle + (sliceStep - gapDeg + lateralExpandDeg) / 2.0;

            DrawSliceByMode(dc, center, item, activeIdx, isSelected: true, startAngle, endAngle, midAngle);
        }

        // Mode 2 overlay: Rotary dial outer ring when an item is selected
        if (_sliderMode == PieSliderMode.RotaryDial && activeIdx >= 0 && activeIdx < count && _items[activeIdx].Category != PieItemCategory.Navigation)
        {
            DrawMode2RotaryOuterRing(dc, center, _items[activeIdx]);
        }

        // Mode 3 overlay: Expanded Outer Arc Slider for the active slice
        if (_sliderMode == PieSliderMode.OuterArcExpand && activeIdx >= 0 && activeIdx < count && _items[activeIdx].Category != PieItemCategory.Navigation)
        {
            double midAngle = GetSliceMidAngle(activeIdx, count);
            DrawMode3ExpandedOuterArc(dc, center, _items[activeIdx], midAngle);
        }

        // Center Hub
        DrawCenterHub(dc, center, activeIdx >= 0 && activeIdx < count ? _items[activeIdx] : null);
    }

    private void DrawNaturalPlatterShadow(DrawingContext dc, System.Windows.Point center, double platterR)
    {
        const double shadowSpread = 26.0;
        double shadowOuterR = platterR + shadowSpread;
        var shadowCenter = new System.Windows.Point(center.X, center.Y + 2.5);

        byte peakAlpha = _palette.IsLight ? (byte)26 : (byte)54;
        byte shadowR = _palette.IsLight ? (byte)15 : (byte)0;
        byte shadowG = _palette.IsLight ? (byte)23 : (byte)0;
        byte shadowB = _palette.IsLight ? (byte)42 : (byte)0;

        double r0 = (platterR - 1.0) / shadowOuterR;
        double span = 1.0 - r0;

        var shadowBrush = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = shadowCenter,
            GradientOrigin = shadowCenter,
            RadiusX = shadowOuterR,
            RadiusY = shadowOuterR
        };

        // Smooth Gaussian decay stops (eliminates linear Mach-band ring artifact)
        (double t, double w)[] gaussianCurve =
        {
            (0.00, 1.00),
            (0.14, 0.78),
            (0.30, 0.52),
            (0.48, 0.28),
            (0.66, 0.12),
            (0.84, 0.03),
            (1.00, 0.00)
        };

        shadowBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, shadowR, shadowG, shadowB), Math.Max(0.0, r0 - 0.01)));
        foreach (var (t, w) in gaussianCurve)
        {
            byte a = (byte)Math.Round(peakAlpha * w);
            shadowBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(a, shadowR, shadowG, shadowB), r0 + t * span));
        }

        // Exclude platter interior so translucent/Acrylic surfaces are never darkened by the drop shadow underneath
        var outerShadowEllipse = new EllipseGeometry(shadowCenter, shadowOuterR, shadowOuterR);
        var platterClip = new EllipseGeometry(center, platterR - 0.5, platterR - 0.5);
        var shadowRingGeo = new CombinedGeometry(GeometryCombineMode.Exclude, outerShadowEllipse, platterClip);
        dc.DrawGeometry(shadowBrush, null, shadowRingGeo);
    }

    private void DrawTexturedGeometry(
        DrawingContext dc,
        Geometry geo,
        System.Windows.Media.Brush baseBrush,
        System.Windows.Media.Pen? borderPen,
        bool isStrongTexture,
        bool applyLightingSheen = false,
        bool isSelected = false)
    {
        if (_surfaceTexture == UiSurfaceTexture.Acrylic)
        {
            if (isStrongTexture)
            {
                // Gauge Fill or Center Hub in Acrylic mode: silky translucent frosted glass layer
                dc.DrawGeometry(baseBrush, null, geo);
                dc.DrawGeometry(ThemeService.GetAcrylicGlassSheenBrush(_palette.IsLight, isSelected), null, geo);
            }
            else
            {
                // Slice pane in Acrylic mode: let the blurred platter backdrop shine through with subtle glass sheen
                byte paneAlpha = (byte)(isSelected ? (_palette.IsLight ? 52 : 28) : (_palette.IsLight ? 20 : 8));
                dc.DrawGeometry(new SolidColorBrush(System.Windows.Media.Color.FromArgb(paneAlpha, 255, 255, 255)), null, geo);
                dc.DrawGeometry(ThemeService.GetAcrylicGlassSheenBrush(_palette.IsLight, isSelected), null, geo);
            }
        }
        else
        {
            dc.DrawGeometry(baseBrush, null, geo);
        }

        if (applyLightingSheen && _lightingEffect != UiLightingEffect.Minimal)
        {
            if (_lightingEffect == UiLightingEffect.PrismAurora)
            {
                byte alpha = (byte)(isSelected ? (_palette.IsLight ? 95 : 120) : (_palette.IsLight ? 60 : 78));
                var prismBrush = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0.0, 0.0),
                    EndPoint = new System.Windows.Point(1.0, 1.0)
                };
                prismBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(alpha, 56, 189, 248), 0.0));
                prismBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(alpha, 168, 85, 247), 0.36));
                prismBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(alpha, 244, 114, 182), 0.68));
                prismBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(alpha, 52, 211, 153), 1.0));
                dc.DrawGeometry(prismBrush, null, geo);
            }
            else if (_lightingEffect == UiLightingEffect.SoftGlow)
            {
                byte spotAlpha = (byte)(isSelected ? (_palette.IsLight ? 75 : 95) : (_palette.IsLight ? 42 : 55));
                var spotBrush = new RadialGradientBrush(
                    WithAlpha(System.Windows.Media.Colors.White, spotAlpha),
                    System.Windows.Media.Colors.Transparent)
                {
                    Center = new System.Windows.Point(0.5, 0.5),
                    GradientOrigin = new System.Windows.Point(0.5, 0.5),
                    RadiusX = 0.55,
                    RadiusY = 0.55
                };
                dc.DrawGeometry(spotBrush, null, geo);
            }
        }

        var texBrush = ThemeService.GetTextureOverlayBrush(_surfaceTexture, _palette.IsLight, isStrongTexture);
        if (texBrush != null)
        {
            dc.DrawGeometry(texBrush, null, geo);
        }

        if (_lightingEffect is UiLightingEffect.EdgeRimGlow or UiLightingEffect.SoftGlow or UiLightingEffect.PrismAurora)
        {
            if (isSelected && borderPen != null)
            {
                var glowCol = _lightingEffect == UiLightingEffect.PrismAurora
                    ? System.Windows.Media.Color.FromRgb(147, 197, 253)
                    : _palette.GlowColor;
                double scale = _lightingEffect == UiLightingEffect.EdgeRimGlow ? 1.0 : 0.65;
                dc.DrawGeometry(null, new System.Windows.Media.Pen(new SolidColorBrush(WithAlpha(glowCol, (byte)(18 * scale))), 10.0), geo);
                dc.DrawGeometry(null, new System.Windows.Media.Pen(new SolidColorBrush(WithAlpha(glowCol, (byte)(34 * scale))), 6.0), geo);
                dc.DrawGeometry(null, new System.Windows.Media.Pen(new SolidColorBrush(WithAlpha(glowCol, (byte)(56 * scale))), 3.2), geo);
            }
        }

        if (borderPen != null)
        {
            if (_surfaceTexture == UiSurfaceTexture.Acrylic)
            {
                var specPen = new System.Windows.Media.Pen(
                    ThemeService.GetAcrylicSpecularBorderBrush(isSelected),
                    isSelected ? 1.5 : 1.0);
                dc.DrawGeometry(null, specPen, geo);
            }
            else
            {
                dc.DrawGeometry(null, borderPen, geo);
            }
        }
    }

    private void DrawLeadingEdgeWithLighting(DrawingContext dc, Geometry edgeArc, System.Windows.Media.Color edgeColor, bool isMuted, bool isSelected)
    {
        if (!isMuted && _lightingEffect != UiLightingEffect.Minimal)
        {
            var glowCol = _lightingEffect == UiLightingEffect.PrismAurora
                ? System.Windows.Media.Color.FromRgb(167, 139, 250)
                : _palette.GlowColor;
            double scale = _lightingEffect == UiLightingEffect.EdgeRimGlow ? 1.0 : (isSelected ? 0.78 : 0.50);
            dc.DrawGeometry(null, new System.Windows.Media.Pen(new SolidColorBrush(WithAlpha(glowCol, (byte)(22 * scale))), 11.0), edgeArc);
            dc.DrawGeometry(null, new System.Windows.Media.Pen(new SolidColorBrush(WithAlpha(glowCol, (byte)(44 * scale))), 6.8), edgeArc);
            dc.DrawGeometry(null, new System.Windows.Media.Pen(new SolidColorBrush(WithAlpha(glowCol, (byte)(72 * scale))), 3.8), edgeArc);
        }
        dc.DrawGeometry(null, new System.Windows.Media.Pen(new SolidColorBrush(edgeColor), 2.4), edgeArc);
    }


    private void DrawSliceByMode(
        DrawingContext dc,
        System.Windows.Point center,
        PieItem item,
        int index,
        bool isSelected,
        double startAngle,
        double endAngle,
        double midAngle)
    {
        switch (_sliderMode)
        {
            case PieSliderMode.RadialGauge:
                DrawMode1RadialSlice(dc, center, item, index, isSelected, startAngle, endAngle, midAngle);
                break;
            case PieSliderMode.RotaryDial:
                DrawMode2RotarySlice(dc, center, item, index, isSelected, startAngle, endAngle, midAngle);
                break;
            case PieSliderMode.OuterArcExpand:
                DrawMode3InnerSlice(dc, center, item, index, isSelected, startAngle, endAngle, midAngle);
                break;
        }
    }

    private int GetSliceIndexFromAngleWithHysteresis(double angleDeg, int count, int currentHoveredIndex)
    {
        if (count <= 1) return 0;
        double step = 360.0 / count;
        double lateralExpandDeg = Math.Clamp(36.0 / count, 4.0, 9.0);

        if (currentHoveredIndex >= 0 && currentHoveredIndex < count)
        {
            double mid = GetSliceMidAngle(currentHoveredIndex, count);
            double diff = Math.Abs(ShortestAngleDiffDeg(mid, angleDeg));
            // Hysteresis: retain current hovered slice across its expanded lateral wings
            if (diff <= (step + lateralExpandDeg) / 2.0)
            {
                return currentHoveredIndex;
            }
        }

        double shifted = NormalizeAngleDeg(angleDeg + 90.0 + step / 2.0);
        int idx = (int)Math.Floor(shifted / step) % count;
        return Math.Clamp(idx, 0, count - 1);
    }

    private void DrawMode1RadialSlice(
        DrawingContext dc,
        System.Windows.Point center,
        PieItem item,
        int index,
        bool isSelected,
        double startAngle,
        double endAngle,
        double midAngle)
    {
        double outerR = isSelected ? StandardOuterR + 7.0 : StandardOuterR;

        // Elevated drop-shadow behind active slice when expanded sideways
        if (isSelected)
        {
            var shadowGeo = CreateAnnularSectorGeometry(center, InnerR - 1.5, outerR + 2.5, startAngle, endAngle);
            byte shadowA = _palette.IsLight ? (byte)32 : (byte)65;
            var shadowBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(shadowA, 0, 0, 0));
            dc.DrawGeometry(null, new System.Windows.Media.Pen(shadowBrush, 4.0), shadowGeo);
        }

        // 1. Base slice track (with Mesh / Acrylic texture)
        var baseGeo = CreateAnnularSectorGeometry(center, InnerR, outerR, startAngle, endAngle);
        var baseColor = isSelected ? _palette.SliceBgSelected : _palette.SliceBg;
        var borderColor = isSelected ? _palette.SliceBorderSelected : _palette.SliceBorder;
        var borderPen = new System.Windows.Media.Pen(new SolidColorBrush(borderColor), isSelected ? 1.8 : 1.0);
        DrawTexturedGeometry(dc, baseGeo, new SolidColorBrush(baseColor), borderPen, isStrongTexture: false, applyLightingSheen: false, isSelected: isSelected);

        if (item.Category != PieItemCategory.Navigation)
        {
            // 2. Subtle 25%, 50%, 75% reference guide rings
            var guidePen = new System.Windows.Media.Pen(new SolidColorBrush(_palette.GuideLine), 1.0)
            {
                DashStyle = DashStyles.Dot
            };
            foreach (double pct in new[] { 0.25, 0.50, 0.75 })
            {
                double gr = InnerR + pct * (outerR - InnerR);
                var arcGeo = CreateArcStrokeGeometry(center, gr, startAngle + 1.0, endAngle - 1.0);
                dc.DrawGeometry(null, guidePen, arcGeo);
            }

            // 3. Radial Gauge Fill (fills the entire slice seamlessly from InnerR outward to Value%)
            double fillRatio = Math.Clamp(item.Value / 100.0, 0.0, 1.0);
            if (fillRatio > 0.005)
            {
                double fillR = InnerR + fillRatio * (outerR - InnerR);
                var fillGeo = CreateAnnularSectorGeometry(center, InnerR, fillR, startAngle, endAngle);

                if (item.IsMuted)
                {
                    DrawTexturedGeometry(dc, fillGeo, new SolidColorBrush(_palette.GaugeMutedFill), null, isStrongTexture: false);
                }
                else if (_surfaceTexture == UiSurfaceTexture.Acrylic)
                {
                    var acrylicFill = ThemeService.GetAcrylicFrostedPillFillBrush(_palette, isSelected);
                    DrawTexturedGeometry(dc, fillGeo, acrylicFill, null, isStrongTexture: true, applyLightingSheen: true, isSelected: isSelected);
                }
                else if (_lightingEffect == UiLightingEffect.Minimal)
                {
                    // Clean flat uniform fill in Minimal mode (zero gradient muddiness)
                    var flatFill = isSelected ? _palette.GaugeFillSelectedEnd : _palette.GaugeFillEnd;
                    DrawTexturedGeometry(dc, fillGeo, new SolidColorBrush(flatFill), null, isStrongTexture: true, applyLightingSheen: false, isSelected: isSelected);
                }
                else
                {
                    // True concentric radial gradient anchored at the Pie Menu center (Absolute coordinates, NOT slice bounding box!)
                    var gradBrush = new RadialGradientBrush
                    {
                        MappingMode = BrushMappingMode.Absolute,
                        Center = center,
                        GradientOrigin = center,
                        RadiusX = outerR,
                        RadiusY = outerR
                    };
                    gradBrush.GradientStops.Add(new GradientStop(isSelected ? _palette.GaugeFillSelectedStart : _palette.GaugeFillStart, InnerR / outerR));
                    gradBrush.GradientStops.Add(new GradientStop(isSelected ? _palette.GaugeFillSelectedEnd : _palette.GaugeFillEnd, fillR / outerR));
                    DrawTexturedGeometry(dc, fillGeo, gradBrush, null, isStrongTexture: true, applyLightingSheen: true, isSelected: isSelected);
                }

                // Crisp leading edge arc at current level with lighting bloom (drawn when not at 100% full)
                if (fillRatio < 0.995)
                {
                    var edgeArc = CreateArcStrokeGeometry(center, fillR, startAngle + 0.5, endAngle - 0.5);
                    var edgeColor = item.IsMuted ? _palette.TextSecondary : _palette.GaugeLeadingEdge;
                    DrawLeadingEdgeWithLighting(dc, edgeArc, edgeColor, item.IsMuted, isSelected);
                }
            }

            // 4. Cursor radial target preview line when hovering this slice
            if (isSelected && !_isHoveringMuteButton && _cursorRadius >= InnerR - 6 && _cursorRadius <= outerR + 12)
            {
                double previewR = Math.Clamp(_cursorRadius, InnerR, outerR);
                var previewArc = CreateArcStrokeGeometry(center, previewR, startAngle + 0.5, endAngle - 0.5);
                var previewPen = new System.Windows.Media.Pen(new SolidColorBrush(WithAlpha(_palette.TextPrimary, 190)), 1.6)
                {
                    DashStyle = DashStyles.Dash
                };
                dc.DrawGeometry(null, previewPen, previewArc);

                if (!_isDraggingSlider)
                {
                    double hoverPct = Math.Round(Math.Clamp((previewR - InnerR) / (outerR - InnerR) * 100.0, 0.0, 100.0));
                    var tagPos = PolarToCartesian(center, previewR, startAngle + 9.0);
                    var tagRect = new Rect(tagPos.X - 22, tagPos.Y - 10, 44, 20);
                    if (_surfaceTexture == UiSurfaceTexture.Acrylic)
                    {
                        var tagGeo = new RectangleGeometry(tagRect, 6, 6);
                        var acrylicBackdrop = _backdropAcrylicBrush ?? ThemeService.GetSimulatedAcrylicBackdropBrush(_palette);
                        dc.DrawGeometry(acrylicBackdrop, null, tagGeo);
                        dc.DrawGeometry(ThemeService.GetAcrylicFrostedPillFillBrush(_palette, true), null, tagGeo);
                        dc.DrawGeometry(ThemeService.GetAcrylicGlassSheenBrush(_palette.IsLight, true), null, tagGeo);
                        dc.DrawGeometry(null, new System.Windows.Media.Pen(ThemeService.GetAcrylicSpecularBorderBrush(true), 1.2), tagGeo);
                    }
                    else
                    {
                        dc.DrawRoundedRectangle(
                            new SolidColorBrush(_palette.ContentPillBg),
                            new System.Windows.Media.Pen(new SolidColorBrush(_palette.SliceBorderSelected), 1.0),
                            tagRect, 6, 6);
                    }
                    DrawCenteredText(dc, $"{hoverPct}%", tagPos, 10.0, BoldTypeface, new SolidColorBrush(_palette.TextPrimary));
                }
            }
        }

        // 5. Slice Content (Icon, Label, Percentage, Mute badge)
        DrawSliceContentAndMuteBadge(dc, center, item, isSelected, midAngle, contentRadius: 162.0, startAngle: startAngle, endAngle: endAngle, maxTextRadius: outerR);
    }

    private void DrawMode2RotarySlice(
        DrawingContext dc,
        System.Windows.Point center,
        PieItem item,
        int index,
        bool isSelected,
        double startAngle,
        double endAngle,
        double midAngle)
    {
        double outerR = isSelected ? 215.0 : 208.0;

        if (isSelected)
        {
            var shadowGeo = CreateAnnularSectorGeometry(center, InnerR - 1.5, outerR + 2.5, startAngle, endAngle);
            byte shadowA = _palette.IsLight ? (byte)32 : (byte)65;
            var shadowBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(shadowA, 0, 0, 0));
            dc.DrawGeometry(null, new System.Windows.Media.Pen(shadowBrush, 4.0), shadowGeo);
        }

        var baseGeo = CreateAnnularSectorGeometry(center, InnerR, outerR, startAngle, endAngle);
        var baseColor = isSelected ? _palette.SliceBgSelected : _palette.SliceBg;
        var borderColor = isSelected ? _palette.SliceBorderSelected : _palette.SliceBorder;
        var borderPen = new System.Windows.Media.Pen(new SolidColorBrush(borderColor), isSelected ? 1.8 : 1.0);

        DrawTexturedGeometry(dc, baseGeo, new SolidColorBrush(baseColor), borderPen, isStrongTexture: false, applyLightingSheen: isSelected, isSelected: isSelected);

        if (item.Category != PieItemCategory.Navigation)
        {
            // Angular progress bar along the outer perimeter of the slice
            double trackInner = outerR - 16.0;
            double trackOuter = outerR - 3.0;
            var trackBgGeo = CreateAnnularSectorGeometry(center, trackInner, trackOuter, startAngle + 2.0, endAngle - 2.0);
            dc.DrawGeometry(new SolidColorBrush(_palette.TrackBackground), null, trackBgGeo);

            double fillRatio = Math.Clamp(item.Value / 100.0, 0.0, 1.0);
            if (fillRatio > 0.005)
            {
                double span = (endAngle - 2.0) - (startAngle + 2.0);
                double valEndAngle = (startAngle + 2.0) + span * fillRatio;
                var fillGeo = CreateAnnularSectorGeometry(center, trackInner, trackOuter, startAngle + 2.0, valEndAngle);
                var fillColor = item.IsMuted ? _palette.GaugeMutedFill : _palette.GaugeFillSelectedEnd;
                DrawTexturedGeometry(dc, fillGeo, new SolidColorBrush(fillColor), null, isStrongTexture: true, applyLightingSheen: !item.IsMuted, isSelected: isSelected);
            }
        }

        DrawSliceContentAndMuteBadge(dc, center, item, isSelected, midAngle, contentRadius: 152.0, startAngle: startAngle, endAngle: endAngle, maxTextRadius: outerR - 16.0);
    }

    private void DrawMode2RotaryOuterRing(DrawingContext dc, System.Windows.Point center, PieItem activeItem)
    {
        double ringR = 230.0;
        var ringPen = new System.Windows.Media.Pen(new SolidColorBrush(WithAlpha(_palette.TextSecondary, 120)), 1.5)
        {
            DashStyle = DashStyles.Dot
        };
        dc.DrawEllipse(null, ringPen, center, ringR, ringR);

        // Draw rotary tick marks around 360 degrees
        for (int deg = 0; deg < 360; deg += 6)
        {
            bool major = (deg % 30 == 0);
            double r1 = ringR - (major ? 5.0 : 2.5);
            double r2 = ringR + (major ? 5.0 : 2.5);
            var p1 = PolarToCartesian(center, r1, deg);
            var p2 = PolarToCartesian(center, r2, deg);
            var tickPen = new System.Windows.Media.Pen(
                new SolidColorBrush(major ? _palette.TextPrimary : WithAlpha(_palette.TextSecondary, 110)),
                major ? 1.6 : 1.0);
            dc.DrawLine(tickPen, p1, p2);
        }

        // Draw cursor rotary handle on the ring
        var knobPos = PolarToCartesian(center, ringR, _cursorAngleDeg);
        DrawSliderThumb(dc, knobPos, 10.5, null);

        // Draw CW (+) and CCW (-) indicator labels near the knob
        var plusPos = PolarToCartesian(center, ringR + 18, _cursorAngleDeg + 14);
        var minusPos = PolarToCartesian(center, ringR + 18, _cursorAngleDeg - 14);
        DrawCenteredText(dc, "+", plusPos, 11.5, BoldTypeface, new SolidColorBrush(_palette.TextPrimary));
        DrawCenteredText(dc, "-", minusPos, 11.5, BoldTypeface, new SolidColorBrush(_palette.TextPrimary));
    }

    private void DrawMode3InnerSlice(
        DrawingContext dc,
        System.Windows.Point center,
        PieItem item,
        int index,
        bool isSelected,
        double startAngle,
        double endAngle,
        double midAngle)
    {
        double outerR = isSelected ? Mode3SliceOuterR + 4.0 : Mode3SliceOuterR;

        if (isSelected)
        {
            var shadowGeo = CreateAnnularSectorGeometry(center, InnerR - 1.5, outerR + 2.0, startAngle, endAngle);
            byte shadowA = _palette.IsLight ? (byte)32 : (byte)65;
            var shadowBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(shadowA, 0, 0, 0));
            dc.DrawGeometry(null, new System.Windows.Media.Pen(shadowBrush, 3.5), shadowGeo);
        }

        var baseGeo = CreateAnnularSectorGeometry(center, InnerR, outerR, startAngle, endAngle);

        var baseColor = isSelected ? _palette.SliceBgSelected : _palette.SliceBg;
        var borderColor = isSelected ? _palette.SliceBorderSelected : _palette.SliceBorder;
        var borderPen = new System.Windows.Media.Pen(new SolidColorBrush(borderColor), isSelected ? 1.8 : 1.0);

        DrawTexturedGeometry(dc, baseGeo, new SolidColorBrush(baseColor), borderPen, isStrongTexture: false, applyLightingSheen: isSelected, isSelected: isSelected);

        if (item.Category != PieItemCategory.Navigation)
        {
            // Subtle mini progress bar near outer edge of inner slice
            double fillRatio = Math.Clamp(item.Value / 100.0, 0.0, 1.0);
            if (fillRatio > 0.005)
            {
                double span = (endAngle - 2.0) - (startAngle + 2.0);
                var miniGeo = CreateAnnularSectorGeometry(center, outerR - 8.0, outerR - 1.5, startAngle + 2.0, startAngle + 2.0 + span * fillRatio);
                var fillColor = item.IsMuted ? _palette.GaugeMutedFill : _palette.GaugeFillSelectedEnd;
                DrawTexturedGeometry(dc, miniGeo, new SolidColorBrush(fillColor), null, isStrongTexture: true, applyLightingSheen: !item.IsMuted, isSelected: isSelected);
            }
        }

        DrawSliceContentAndMuteBadge(dc, center, item, isSelected, midAngle, contentRadius: 138.0, startAngle: startAngle, endAngle: endAngle, maxTextRadius: outerR - 8.0);
    }

    private void DrawMode3ExpandedOuterArc(DrawingContext dc, System.Windows.Point center, PieItem item, double midAngle)
    {
        double halfSpan = Mode3ArcSpanDeg / 2.0;
        double arcStart = midAngle - halfSpan;
        double arcEnd = midAngle + halfSpan;

        // Outer Arc Background Container (with Mesh / Acrylic texture)
        var outerContainerGeo = CreateAnnularSectorGeometry(center, Mode3ArcInnerR, Mode3ArcOuterR, arcStart, arcEnd);

        // Soft multi-layer exterior ambient shadow for the expanded outer arc (clipped outside container)
        var shadowClip = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)),
            outerContainerGeo);
        dc.PushClip(shadowClip);
        var shadowCol = _palette.IsLight
            ? System.Windows.Media.Color.FromRgb(15, 23, 42)
            : System.Windows.Media.Color.FromRgb(0, 0, 0);
        double sScale = _palette.IsLight ? 1.0 : 1.8;
        dc.DrawGeometry(null, new System.Windows.Media.Pen(new SolidColorBrush(WithAlpha(shadowCol, (byte)(4 * sScale))), 24.0), outerContainerGeo);
        dc.DrawGeometry(null, new System.Windows.Media.Pen(new SolidColorBrush(WithAlpha(shadowCol, (byte)(8 * sScale))), 14.0), outerContainerGeo);
        dc.DrawGeometry(null, new System.Windows.Media.Pen(new SolidColorBrush(WithAlpha(shadowCol, (byte)(14 * sScale))), 6.0), outerContainerGeo);
        dc.Pop();

        if (_surfaceTexture == UiSurfaceTexture.Acrylic)
        {
            var acrylicBackdrop = _backdropAcrylicBrush ?? ThemeService.GetSimulatedAcrylicBackdropBrush(_palette);
            dc.DrawGeometry(acrylicBackdrop, null, outerContainerGeo);
        }
        DrawTexturedGeometry(
            dc,
            outerContainerGeo,
            new SolidColorBrush(_palette.SliceBgSelected),
            new System.Windows.Media.Pen(new SolidColorBrush(_palette.SliceBorderSelected), 1.8),
            isStrongTexture: false,
            applyLightingSheen: false,
            isSelected: true);

        // Usable Slider Track inside Outer Arc
        double trackStart = arcStart + 6.0;
        double trackEnd = arcEnd - 6.0;
        double trackSpan = trackEnd - trackStart;

        var trackBgGeo = CreateAnnularSectorGeometry(center, Mode3ArcCenterR - 7.5, Mode3ArcCenterR + 7.5, trackStart, trackEnd);
        var trackBgBrush = _surfaceTexture == UiSurfaceTexture.Acrylic
            ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(_palette.IsLight ? (byte)32 : (byte)48, 0, 0, 0))
            : new SolidColorBrush(_palette.TrackBackground);
        dc.DrawGeometry(trackBgBrush, null, trackBgGeo);

        // Graduation ticks at 0%, 25%, 50%, 75%, 100%
        foreach (double tickPct in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
        {
            double tickAngle = trackStart + tickPct * trackSpan;
            var p1 = PolarToCartesian(center, Mode3ArcInnerR + 4, tickAngle);
            var p2 = PolarToCartesian(center, Mode3ArcOuterR - 4, tickAngle);
            dc.DrawLine(new System.Windows.Media.Pen(new SolidColorBrush(_palette.GuideLine), 1.0), p1, p2);
        }

        // Filled Portion of Outer Arc
        double fillRatio = Math.Clamp(item.Value / 100.0, 0.0, 1.0);
        double thumbAngle = trackStart + fillRatio * trackSpan;
        if (fillRatio > 0.005)
        {
            var fillGeo = CreateAnnularSectorGeometry(center, Mode3ArcCenterR - 7.5, Mode3ArcCenterR + 7.5, trackStart, thumbAngle);
            System.Windows.Media.Brush fillBrush = item.IsMuted
                ? new SolidColorBrush(_palette.GaugeMutedFill)
                : (_surfaceTexture == UiSurfaceTexture.Acrylic
                    ? ThemeService.GetAcrylicFrostedPillFillBrush(_palette, true)
                    : new SolidColorBrush(_palette.GaugeFillSelectedEnd));
            DrawTexturedGeometry(dc, fillGeo, fillBrush, null, isStrongTexture: true, applyLightingSheen: !item.IsMuted, isSelected: true);
        }

        // Draggable Circular Thumb Handle
        var thumbPos = PolarToCartesian(center, Mode3ArcCenterR, thumbAngle);
        string thumbText = $"{Math.Round(item.Value)}";
        DrawSliderThumb(dc, thumbPos, 15.0, thumbText);
    }

    private void DrawSliderThumb(DrawingContext dc, System.Windows.Point thumbPos, double radius, string? labelText)
    {
        var thumbGeo = new EllipseGeometry(thumbPos, radius, radius);

        if (_surfaceTexture == UiSurfaceTexture.Acrylic)
        {
            // Soft exterior drop shadow around the glass thumb
            double shadowOuterR = radius + 6.0;
            var shadowBrush = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                Center = new System.Windows.Point(thumbPos.X, thumbPos.Y + 1.2),
                GradientOrigin = new System.Windows.Point(thumbPos.X, thumbPos.Y + 1.2),
                RadiusX = shadowOuterR,
                RadiusY = shadowOuterR
            };
            byte peakA = _palette.IsLight ? (byte)32 : (byte)64;
            double r0 = (radius - 0.5) / shadowOuterR;
            shadowBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, 0, 0, 0), Math.Max(0.0, r0 - 0.02)));
            shadowBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(peakA, 0, 0, 0), r0));
            shadowBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb((byte)(peakA * 0.45), 0, 0, 0), r0 + (1.0 - r0) * 0.45));
            shadowBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, 0, 0, 0), 1.0));

            var shadowRing = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                new EllipseGeometry(new System.Windows.Point(thumbPos.X, thumbPos.Y + 1.2), shadowOuterR, shadowOuterR),
                new EllipseGeometry(thumbPos, radius - 0.4, radius - 0.4));
            dc.DrawGeometry(shadowBrush, null, shadowRing);

            // Frosted Acrylic Glass Thumb Layers
            var acrylicBackdrop = _backdropAcrylicBrush ?? ThemeService.GetSimulatedAcrylicBackdropBrush(_palette);
            dc.DrawGeometry(acrylicBackdrop, null, thumbGeo);
            dc.DrawGeometry(ThemeService.GetAcrylicFrostedPillFillBrush(_palette, true), null, thumbGeo);
            byte frostBoost = _palette.IsLight ? (byte)95 : (byte)42;
            dc.DrawGeometry(new SolidColorBrush(System.Windows.Media.Color.FromArgb(frostBoost, 255, 255, 255)), null, thumbGeo);
            dc.DrawGeometry(ThemeService.GetAcrylicGlassSheenBrush(_palette.IsLight, true), null, thumbGeo);
            dc.DrawGeometry(null, new System.Windows.Media.Pen(ThemeService.GetAcrylicSpecularBorderBrush(true), 1.6), thumbGeo);

            if (!string.IsNullOrEmpty(labelText))
            {
                DrawCenteredText(dc, labelText, thumbPos, 10.5, BoldTypeface, new SolidColorBrush(_palette.TextPrimary));
            }
        }
        else
        {
            dc.DrawGeometry(new SolidColorBrush(_palette.ThumbFill), new System.Windows.Media.Pen(new SolidColorBrush(_palette.ThumbBorder), 2.0), thumbGeo);

            if (!string.IsNullOrEmpty(labelText))
            {
                DrawCenteredText(dc, labelText, thumbPos, 10.5, BoldTypeface, new SolidColorBrush(_palette.ThumbText));
            }
        }
    }

    private void DrawSliceContentAndMuteBadge(
        DrawingContext dc,
        System.Windows.Point center,
        PieItem item,
        bool isSelected,
        double midAngle,
        double contentRadius,
        double startAngle,
        double endAngle,
        double maxTextRadius)
    {
        // 1. Compact Vector Mute Badge near inner ring (for audio items)
        if (item.CanMute)
        {
            var mutePos = PolarToCartesian(center, MuteBadgeRadius, midAngle);
            bool muteHovered = isSelected && _isHoveringMuteButton;
            System.Windows.Media.Brush muteBg;
            System.Windows.Media.Pen muteBorder;
            if (_surfaceTexture == UiSurfaceTexture.Acrylic)
            {
                muteBg = item.IsMuted
                    ? new SolidColorBrush(WithAlpha(_palette.MuteBadgeBgActive, 175))
                    : ThemeService.GetAcrylicFrostedPillFillBrush(_palette, muteHovered || isSelected);
                muteBorder = new System.Windows.Media.Pen(
                    ThemeService.GetAcrylicSpecularBorderBrush(muteHovered || isSelected),
                    muteHovered ? 1.5 : 1.0);
            }
            else
            {
                muteBg = item.IsMuted
                    ? new SolidColorBrush(_palette.MuteBadgeBgActive)
                    : new SolidColorBrush(muteHovered ? _palette.MuteBadgeBgHover : _palette.MuteBadgeBg);
                muteBorder = new System.Windows.Media.Pen(
                    new SolidColorBrush(muteHovered ? _palette.TextPrimary : _palette.MuteBadgeBorder),
                    muteHovered ? 1.5 : 1.0);
            }

            dc.DrawEllipse(muteBg, muteBorder, mutePos, 12.0, 12.0);
            var speakerColor = isSelected || muteHovered
                ? _palette.TextPrimary
                : WithAlpha(_palette.TextPrimary, 210);
            DrawVectorSpeakerIcon(dc, mutePos, item.IsMuted, new SolidColorBrush(speakerColor));
        }

        // 2. Clean Un-Outlined 2-Line Slice Typography (matches Center Hub aesthetic)
        var contentPos = PolarToCartesian(center, contentRadius, midAngle);
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        if (_sliceLabelStyle == SliceLabelStyle.IconOnly)
        {
            DrawIconOnlySliceContent(dc, item, contentPos, isSelected, pixelsPerDip);
            return;
        }

        var titleRowY = contentPos.Y - 8.5;
        string titleStr = TruncateText(item.Title, item.AppIcon != null ? 9 : 10);
        var titleColor = isSelected ? _palette.TextPrimary : WithAlpha(_palette.TextPrimary, 215);
        var titleTypeface = isSelected ? UiTypeface : MediumTypeface;
        const double iconSize = 14.0;
        const double gap = 4.5;
        double iconExtraW = item.AppIcon != null ? iconSize + gap : 0.0;

        // Fit the title row inside the actual wedge shape (diagonal slices are narrower for horizontal text):
        // first shrink the font slightly, then truncate with an ellipsis until all 4 corners lie inside the slice.
        FormattedText titleFt = CreateTitleText(titleStr, titleTypeface, 12.5, titleColor, pixelsPerDip);
        if (!IsRowInsideSlice(center, contentPos.X, titleRowY, iconExtraW + titleFt.Width, Math.Max(titleFt.Height, iconExtraW > 0 ? iconSize : 0), startAngle, endAngle, maxTextRadius))
        {
            bool fitted = false;
            foreach (double size in new[] { 11.8, 11.0 })
            {
                titleFt = CreateTitleText(titleStr, titleTypeface, size, titleColor, pixelsPerDip);
                if (IsRowInsideSlice(center, contentPos.X, titleRowY, iconExtraW + titleFt.Width, Math.Max(titleFt.Height, iconExtraW > 0 ? iconSize : 0), startAngle, endAngle, maxTextRadius))
                {
                    fitted = true;
                    break;
                }
            }

            if (!fitted)
            {
                string baseText = item.Title ?? string.Empty;
                for (int len = Math.Min(baseText.Length, titleStr.TrimEnd('…').Length) - 1; len >= 1; len--)
                {
                    string candidate = baseText[..len] + "…";
                    titleFt = CreateTitleText(candidate, titleTypeface, 11.0, titleColor, pixelsPerDip);
                    if (IsRowInsideSlice(center, contentPos.X, titleRowY, iconExtraW + titleFt.Width, Math.Max(titleFt.Height, iconExtraW > 0 ? iconSize : 0), startAngle, endAngle, maxTextRadius))
                    {
                        break;
                    }
                }
            }
        }

        if (item.AppIcon != null)
        {
            double totalW = iconSize + gap + titleFt.Width;
            double startX = contentPos.X - totalW / 2.0;

            var iconRect = new Rect(startX, titleRowY - iconSize / 2.0, iconSize, iconSize);
            dc.DrawImage(item.AppIcon, iconRect);

            var textPt = new System.Windows.Point(startX + iconSize + gap, titleRowY - titleFt.Height / 2.0);
            dc.DrawText(titleFt, textPt);
        }
        else
        {
            var textPt = new System.Windows.Point(contentPos.X - titleFt.Width / 2.0, titleRowY - titleFt.Height / 2.0);
            dc.DrawText(titleFt, textPt);
        }

        var valRowY = contentPos.Y + 8.5;
        string valStr = item.Category == PieItemCategory.Navigation
            ? (item.CustomDisplayValue ?? "開く ▶")
            : (item.IsMuted ? "MUTE" : $"{Math.Round(item.Value)}%");
        var valColor = item.Category == PieItemCategory.Navigation
            ? (isSelected ? _palette.Accent : WithAlpha(_palette.TextPrimary, 220))
            : (item.IsMuted ? _palette.TextMutedAlert : (isSelected ? _palette.TextPrimary : WithAlpha(_palette.TextPrimary, 220)));
        var valTypeface = isSelected ? BoldTypeface : UiTypeface;
        var valFt = new FormattedText(
            valStr,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            valTypeface,
            isSelected ? 14.5 : 13.5,
            new SolidColorBrush(valColor),
            pixelsPerDip);
        var valPt = new System.Windows.Point(contentPos.X - valFt.Width / 2.0, valRowY - valFt.Height / 2.0);
        dc.DrawText(valFt, valPt);
    }

    private void DrawIconOnlySliceContent(
        DrawingContext dc,
        PieItem item,
        System.Windows.Point contentPos,
        bool isSelected,
        double pixelsPerDip)
    {
        // Prominent centered icon + value below (ultra-clean minimalist look)
        const double iconSize = 30.0;
        var iconCenter = new System.Windows.Point(contentPos.X, contentPos.Y - 9.5);

        if (item.AppIcon != null)
        {
            var iconRect = new Rect(iconCenter.X - iconSize / 2.0, iconCenter.Y - iconSize / 2.0, iconSize, iconSize);
            dc.DrawImage(item.AppIcon, iconRect);
        }
        else
        {
            var iconBrush = new SolidColorBrush(isSelected ? _palette.TextPrimary : WithAlpha(_palette.TextPrimary, 225));
            switch (item.Category)
            {
                case PieItemCategory.MasterVolume:
                    DrawMasterVolumeVectorIcon(dc, iconCenter, iconSize, iconBrush, item.IsMuted);
                    break;
                case PieItemCategory.Brightness:
                    DrawBrightnessVectorIcon(dc, iconCenter, iconSize, iconBrush);
                    break;
                case PieItemCategory.MicVolume:
                    DrawMicrophoneVectorIcon(dc, iconCenter, iconSize, iconBrush, item.IsMuted);
                    break;
                case PieItemCategory.SystemSounds:
                    DrawSystemSoundsVectorIcon(dc, iconCenter, iconSize, iconBrush, item.IsMuted);
                    break;
                case PieItemCategory.Navigation:
                    DrawAppsGridVectorIcon(dc, iconCenter, iconSize, iconBrush);
                    break;
                default:
                    DrawMasterVolumeVectorIcon(dc, iconCenter, iconSize, iconBrush, item.IsMuted);
                    break;
            }
        }

        var valRowY = contentPos.Y + 14.5;
        string valStr = item.Category == PieItemCategory.Navigation
            ? (item.CustomDisplayValue ?? "一覧 ▶")
            : (item.IsMuted ? "MUTE" : $"{Math.Round(item.Value)}%");
        var valColor = item.Category == PieItemCategory.Navigation
            ? (isSelected ? _palette.Accent : WithAlpha(_palette.TextPrimary, 225))
            : (item.IsMuted ? _palette.TextMutedAlert : (isSelected ? _palette.TextPrimary : WithAlpha(_palette.TextPrimary, 225)));
        var valTypeface = BoldTypeface;
        var valFt = new FormattedText(
            valStr,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            valTypeface,
            isSelected ? 16.0 : 14.8,
            new SolidColorBrush(valColor),
            pixelsPerDip);
        var valPt = new System.Windows.Point(contentPos.X - valFt.Width / 2.0, valRowY - valFt.Height / 2.0);
        dc.DrawText(valFt, valPt);
    }

    private static void DrawVectorSpeakerIcon(
        DrawingContext dc,
        System.Windows.Point center,
        bool isMuted,
        System.Windows.Media.Brush brush)
    {
        // Speaker cone polygon centered inside the 23px badge
        var coneGeo = new StreamGeometry();
        using (var ctx = coneGeo.Open())
        {
            ctx.BeginFigure(new System.Windows.Point(center.X - 4.8, center.Y - 1.8), isFilled: true, isClosed: true);
            ctx.LineTo(new System.Windows.Point(center.X - 2.3, center.Y - 1.8), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new System.Windows.Point(center.X + 0.6, center.Y - 4.4), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new System.Windows.Point(center.X + 0.6, center.Y + 4.4), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new System.Windows.Point(center.X - 2.3, center.Y + 1.8), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new System.Windows.Point(center.X - 4.8, center.Y + 1.8), isStroked: false, isSmoothJoin: false);
        }
        coneGeo.Freeze();
        dc.DrawGeometry(brush, null, coneGeo);

        var pen = new System.Windows.Media.Pen(brush, 1.35)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        if (isMuted)
        {
            dc.DrawLine(pen, new System.Windows.Point(center.X + 2.2, center.Y - 2.2), new System.Windows.Point(center.X + 5.4, center.Y + 2.2));
            dc.DrawLine(pen, new System.Windows.Point(center.X + 5.4, center.Y - 2.2), new System.Windows.Point(center.X + 2.2, center.Y + 2.2));
        }
        else
        {
            var wave1 = CreateArcStrokeGeometry(new System.Windows.Point(center.X + 0.2, center.Y), 2.8, -42, 42);
            var wave2 = CreateArcStrokeGeometry(new System.Windows.Point(center.X + 0.2, center.Y), 4.8, -45, 45);
            dc.DrawGeometry(null, pen, wave1);
            dc.DrawGeometry(null, pen, wave2);
        }
    }

    private static void DrawMasterVolumeVectorIcon(DrawingContext dc, System.Windows.Point center, double size, System.Windows.Media.Brush brush, bool isMuted)
    {
        double s = size / 24.0;
        var coneGeo = new StreamGeometry();
        using (var ctx = coneGeo.Open())
        {
            ctx.BeginFigure(new System.Windows.Point(center.X - 6.4 * s, center.Y - 2.5 * s), isFilled: true, isClosed: true);
            ctx.LineTo(new System.Windows.Point(center.X - 2.8 * s, center.Y - 2.5 * s), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new System.Windows.Point(center.X + 1.4 * s, center.Y - 6.2 * s), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new System.Windows.Point(center.X + 1.4 * s, center.Y + 6.2 * s), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new System.Windows.Point(center.X - 2.8 * s, center.Y + 2.5 * s), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new System.Windows.Point(center.X - 6.4 * s, center.Y + 2.5 * s), isStroked: false, isSmoothJoin: false);
        }
        coneGeo.Freeze();
        dc.DrawGeometry(brush, null, coneGeo);

        var pen = new System.Windows.Media.Pen(brush, 1.6 * s)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        if (isMuted)
        {
            var mutePen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 113, 113)), 1.8 * s)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawLine(mutePen, new System.Windows.Point(center.X + 3.0 * s, center.Y - 3.5 * s), new System.Windows.Point(center.X + 8.5 * s, center.Y + 3.5 * s));
            dc.DrawLine(mutePen, new System.Windows.Point(center.X + 8.5 * s, center.Y - 3.5 * s), new System.Windows.Point(center.X + 3.0 * s, center.Y + 3.5 * s));
        }
        else
        {
            var wave1 = CreateArcStrokeGeometry(new System.Windows.Point(center.X - 2.0 * s, center.Y), 6.5 * s, -42, 42);
            var wave2 = CreateArcStrokeGeometry(new System.Windows.Point(center.X - 2.0 * s, center.Y), 10.2 * s, -45, 45);
            dc.DrawGeometry(null, pen, wave1);
            dc.DrawGeometry(null, pen, wave2);
        }
    }

    private static void DrawBrightnessVectorIcon(DrawingContext dc, System.Windows.Point center, double size, System.Windows.Media.Brush brush)
    {
        double r = size * 0.22;
        dc.DrawEllipse(brush, null, center, r, r);
        var pen = new System.Windows.Media.Pen(brush, 1.8)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        for (int a = 0; a < 360; a += 45)
        {
            double rad = a * Math.PI / 180.0;
            var p1 = new System.Windows.Point(center.X + Math.Cos(rad) * (size * 0.36), center.Y + Math.Sin(rad) * (size * 0.36));
            var p2 = new System.Windows.Point(center.X + Math.Cos(rad) * (size * 0.49), center.Y + Math.Sin(rad) * (size * 0.49));
            dc.DrawLine(pen, p1, p2);
        }
    }

    private static void DrawMicrophoneVectorIcon(DrawingContext dc, System.Windows.Point center, double size, System.Windows.Media.Brush brush, bool isMuted)
    {
        double s = size / 24.0;
        var pillRect = new Rect(center.X - 3.4 * s, center.Y - 7.5 * s, 6.8 * s, 10.5 * s);
        dc.DrawRoundedRectangle(brush, null, pillRect, 3.4 * s, 3.4 * s);

        var pen = new System.Windows.Media.Pen(brush, 1.6 * s)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        var cradleArc = CreateArcStrokeGeometry(new System.Windows.Point(center.X, center.Y - 2.0 * s), 6.2 * s, 10, 170);
        dc.DrawGeometry(null, pen, cradleArc);
        dc.DrawLine(pen, new System.Windows.Point(center.X, center.Y + 4.2 * s), new System.Windows.Point(center.X, center.Y + 8.2 * s));
        dc.DrawLine(pen, new System.Windows.Point(center.X - 4.5 * s, center.Y + 8.2 * s), new System.Windows.Point(center.X + 4.5 * s, center.Y + 8.2 * s));

        if (isMuted)
        {
            var mutePen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 113, 113)), 1.8 * s)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawLine(mutePen, new System.Windows.Point(center.X - 6.5 * s, center.Y - 6.5 * s), new System.Windows.Point(center.X + 6.5 * s, center.Y + 6.5 * s));
        }
    }

    private static void DrawSystemSoundsVectorIcon(DrawingContext dc, System.Windows.Point center, double size, System.Windows.Media.Brush brush, bool isMuted)
    {
        double s = size / 24.0;

        // Clean notification bell dome and rim
        var bellGeo = new StreamGeometry();
        using (var ctx = bellGeo.Open())
        {
            ctx.BeginFigure(new System.Windows.Point(center.X - 1.2 * s, center.Y - 8.5 * s), isFilled: true, isClosed: true);
            ctx.LineTo(new System.Windows.Point(center.X + 1.2 * s, center.Y - 8.5 * s), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new System.Windows.Point(center.X + 1.2 * s, center.Y - 6.5 * s), isStroked: false, isSmoothJoin: false);
            ctx.BezierTo(
                new System.Windows.Point(center.X + 6.0 * s, center.Y - 4.0 * s),
                new System.Windows.Point(center.X + 7.5 * s, center.Y + 2.0 * s),
                new System.Windows.Point(center.X + 8.5 * s, center.Y + 4.5 * s),
                isStroked: false, isSmoothJoin: true);
            ctx.LineTo(new System.Windows.Point(center.X - 8.5 * s, center.Y + 4.5 * s), isStroked: false, isSmoothJoin: false);
            ctx.BezierTo(
                new System.Windows.Point(center.X - 7.5 * s, center.Y + 2.0 * s),
                new System.Windows.Point(center.X - 6.0 * s, center.Y - 4.0 * s),
                new System.Windows.Point(center.X - 1.2 * s, center.Y - 6.5 * s),
                isStroked: false, isSmoothJoin: true);
        }
        bellGeo.Freeze();
        dc.DrawGeometry(brush, null, bellGeo);

        // Clapper underneath bell
        dc.DrawEllipse(brush, null, new System.Windows.Point(center.X, center.Y + 6.5 * s), 2.2 * s, 2.2 * s);

        if (isMuted)
        {
            var mutePen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 113, 113)), 1.8 * s)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawLine(mutePen, new System.Windows.Point(center.X - 7.5 * s, center.Y - 7.5 * s), new System.Windows.Point(center.X + 7.5 * s, center.Y + 7.5 * s));
        }
    }

    private static void DrawAppsGridVectorIcon(DrawingContext dc, System.Windows.Point center, double size, System.Windows.Media.Brush brush)
    {
        double s = size / 24.0;
        double boxSize = 7.0 * s;
        double radius = 2.0 * s;
        double gap = 3.0 * s;

        // 4 small rounded tiles in a 2x2 grid
        dc.DrawRoundedRectangle(brush, null, new Rect(center.X - boxSize - gap / 2.0, center.Y - boxSize - gap / 2.0, boxSize, boxSize), radius, radius);
        dc.DrawRoundedRectangle(brush, null, new Rect(center.X + gap / 2.0, center.Y - boxSize - gap / 2.0, boxSize, boxSize), radius, radius);
        dc.DrawRoundedRectangle(brush, null, new Rect(center.X - boxSize - gap / 2.0, center.Y + gap / 2.0, boxSize, boxSize), radius, radius);
        dc.DrawRoundedRectangle(brush, null, new Rect(center.X + gap / 2.0, center.Y + gap / 2.0, boxSize, boxSize), radius, radius);
    }

    private void DrawCenterHub(DrawingContext dc, System.Windows.Point center, PieItem? activeItem)
    {
        var hubGeo = new EllipseGeometry(center, CenterR, CenterR);
        bool isSubHover = IsInSubPage && _isHoveringCenterHub;
        if (_surfaceTexture == UiSurfaceTexture.Acrylic)
        {
            var acrylicBackdrop = _backdropAcrylicBrush ?? ThemeService.GetSimulatedAcrylicBackdropBrush(_palette);
            byte hubFrostAlpha = (byte)(_palette.IsLight ? 44 : 18);
            dc.DrawGeometry(acrylicBackdrop, null, hubGeo);
            dc.DrawGeometry(new SolidColorBrush(System.Windows.Media.Color.FromArgb(hubFrostAlpha, 255, 255, 255)), null, hubGeo);
            dc.DrawGeometry(ThemeService.GetAcrylicGlassSheenBrush(_palette.IsLight, true), null, hubGeo);
            System.Windows.Media.Brush hubBorderBrush = isSubHover
                ? new SolidColorBrush(_palette.Accent)
                : ThemeService.GetAcrylicSpecularBorderBrush(true);
            dc.DrawGeometry(null, new System.Windows.Media.Pen(hubBorderBrush, isSubHover ? 2.4 : 1.6), hubGeo);
        }
        else
        {
            System.Windows.Media.Brush hubBrush;
            if (isSubHover)
            {
                hubBrush = new RadialGradientBrush(WithAlpha(_palette.Accent, 80), _palette.CenterHubEnd);
            }
            else
            {
                hubBrush = _lightingEffect == UiLightingEffect.Minimal
                    ? new SolidColorBrush(_palette.CenterHubEnd)
                    : new RadialGradientBrush(_palette.CenterHubStart, _palette.CenterHubEnd);
            }
            var hubPenBrush = isSubHover
                ? new SolidColorBrush(_palette.Accent)
                : new SolidColorBrush(_palette.CenterHubBorder);
            var hubPen = new System.Windows.Media.Pen(hubPenBrush, isSubHover ? 2.6 : 2.0);
            DrawTexturedGeometry(
                dc,
                hubGeo,
                hubBrush,
                hubPen,
                isStrongTexture: true,
                applyLightingSheen: activeItem != null || isSubHover,
                isSelected: activeItem != null || isSubHover);
        }

        // Circular progress arc inside the center hub perimeter
        if (activeItem != null)
        {
            if (activeItem.Category == PieItemCategory.Navigation)
            {
                DrawCenteredText(
                    dc,
                    "アプリ音量",
                    new System.Windows.Point(center.X, center.Y - 18),
                    13.0,
                    MediumTypeface,
                    new SolidColorBrush(WithAlpha(_palette.TextPrimary, 225)));

                DrawCenteredText(
                    dc,
                    activeItem.CustomDisplayValue ?? "一覧 ▶",
                    new System.Windows.Point(center.X, center.Y + 1),
                    19.0,
                    BoldTypeface,
                    new SolidColorBrush(_palette.Accent));

                DrawCenteredText(
                    dc,
                    "クリックして一覧を開く",
                    new System.Windows.Point(center.X, center.Y + 22),
                    10.8,
                    UiTypeface,
                    new SolidColorBrush(_palette.TextSecondary));
            }
            else
            {
                double pct = Math.Clamp(activeItem.Value / 100.0, 0.0, 1.0);
                if (pct > 0.01)
                {
                    var ringGeo = CreateArcStrokeGeometry(center, CenterR - 5.0, -90.0, -90.0 + pct * 359.5);
                    var ringColor = activeItem.IsMuted ? _palette.TextMutedAlert : _palette.CenterHubRing;
                    dc.DrawGeometry(null, new System.Windows.Media.Pen(new SolidColorBrush(ringColor), 3.2), ringGeo);
                }

                DrawCenteredText(
                    dc,
                    TruncateText(activeItem.Title, 11),
                    new System.Windows.Point(center.X, center.Y - 24),
                    12.5,
                    MediumTypeface,
                    new SolidColorBrush(WithAlpha(_palette.TextPrimary, 225)));

                string mainReadout = activeItem.IsMuted ? "MUTE" : $"{Math.Round(activeItem.Value)}%";
                var mainBrush = activeItem.IsMuted
                    ? new SolidColorBrush(_palette.TextMutedAlert)
                    : new SolidColorBrush(_palette.TextPrimary);

                DrawCenteredText(
                    dc,
                    mainReadout,
                    new System.Windows.Point(center.X, center.Y + 1),
                    24.0,
                    BoldTypeface,
                    mainBrush);

                string hint;
                if (IsInSubPage)
                {
                    hint = "中央または右クリックで戻る";
                }
                else if (_isDraggingSlider)
                {
                    hint = "ボタンを離すとこの値で確定";
                }
                else
                {
                    hint = _sliderMode switch
                    {
                        PieSliderMode.RadialGauge => "左ドラッグ / ホイールで調整",
                        PieSliderMode.RotaryDial => "円周ドラッグ / ホイールで調整",
                        PieSliderMode.OuterArcExpand => "外周バーを左ドラッグ",
                        _ => ""
                    };
                }

                DrawCenteredText(
                    dc,
                    hint,
                    new System.Windows.Point(center.X, center.Y + 24),
                    10.8,
                    MediumTypeface,
                    new SolidColorBrush(WithAlpha(_palette.TextPrimary, 185)));
            }
        }
        else
        {
            if (IsInSubPage)
            {
                bool hoverHub = _isHoveringCenterHub;
                var backBrush = new SolidColorBrush(hoverHub ? _palette.Accent : _palette.TextPrimary);
                var subBrush = new SolidColorBrush(hoverHub ? _palette.TextPrimary : _palette.TextSecondary);

                DrawCenteredText(
                    dc,
                    "◀ 戻る",
                    new System.Windows.Point(center.X, center.Y - 14),
                    16.0,
                    BoldTypeface,
                    backBrush);

                DrawCenteredText(
                    dc,
                    "アプリ音量一覧",
                    new System.Windows.Point(center.X, center.Y + 6),
                    12.0,
                    MediumTypeface,
                    new SolidColorBrush(_palette.TextPrimary));

                DrawCenteredText(
                    dc,
                    "クリックでメインへ",
                    new System.Windows.Point(center.X, center.Y + 22),
                    10.8,
                    UiTypeface,
                    subBrush);
            }
            else
            {
                DrawCenteredText(
                    dc,
                    "VolumePie",
                    new System.Windows.Point(center.X, center.Y - 16),
                    14.5,
                    BoldTypeface,
                    new SolidColorBrush(_palette.TextPrimary));

                string modeShort = _sliderMode switch
                {
                    PieSliderMode.RadialGauge => "ラジアル型",
                    PieSliderMode.RotaryDial => "ダイヤル型",
                    PieSliderMode.OuterArcExpand => "外周アーク型",
                    _ => ""
                };

                DrawCenteredText(
                    dc,
                    modeShort,
                    new System.Windows.Point(center.X, center.Y + 4),
                    12.5,
                    BoldTypeface,
                    new SolidColorBrush(_palette.TextPrimary));

                DrawCenteredText(
                    dc,
                    "中央クリックで閉じる",
                    new System.Windows.Point(center.X, center.Y + 22),
                    10.8,
                    UiTypeface,
                    new SolidColorBrush(_palette.TextSecondary));
            }
        }
    }

    private bool CheckMuteBadgeHit(int sliceIndex, System.Windows.Point center, System.Windows.Point cursor)
    {
        if (sliceIndex < 0 || sliceIndex >= _items.Count) return false;
        if (!_items[sliceIndex].CanMute) return false;

        double midAngle = GetSliceMidAngle(sliceIndex, _items.Count);
        var muteCenter = PolarToCartesian(center, MuteBadgeRadius, midAngle);
        double dx = cursor.X - muteCenter.X;
        double dy = cursor.Y - muteCenter.Y;
        return (dx * dx + dy * dy) <= (14.0 * 14.0);
    }

    private static double GetSliceMidAngle(int index, int count)
    {
        double step = 360.0 / count;
        return -90.0 + index * step;
    }

    private static int GetSliceIndexFromAngle(double angleDeg, int count)
    {
        double step = 360.0 / count;
        // Shift so slice 0 is centered at -90 deg (top)
        double shifted = NormalizeAngleDeg(angleDeg + 90.0 + step / 2.0);
        int idx = (int)Math.Floor(shifted / step) % count;
        return Math.Clamp(idx, 0, count - 1);
    }

    private static StreamGeometry CreateAnnularSectorGeometry(
        System.Windows.Point center,
        double innerRadius,
        double outerRadius,
        double startAngleDeg,
        double endAngleDeg)
    {
        var geo = new StreamGeometry();
        double span = Math.Clamp(endAngleDeg - startAngleDeg, 0.1, 359.8);
        endAngleDeg = startAngleDeg + span;
        bool isLargeArc = span > 180.0;

        var p1 = PolarToCartesian(center, outerRadius, startAngleDeg);
        var p2 = PolarToCartesian(center, outerRadius, endAngleDeg);
        var p3 = PolarToCartesian(center, innerRadius, endAngleDeg);
        var p4 = PolarToCartesian(center, innerRadius, startAngleDeg);

        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(p1, isFilled: true, isClosed: true);
            ctx.ArcTo(p2, new System.Windows.Size(outerRadius, outerRadius), 0, isLargeArc, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(p3, isStroked: true, isSmoothJoin: false);
            ctx.ArcTo(p4, new System.Windows.Size(innerRadius, innerRadius), 0, isLargeArc, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: false);
        }
        geo.Freeze();
        return geo;
    }

    private static StreamGeometry CreateArcStrokeGeometry(
        System.Windows.Point center,
        double radius,
        double startAngleDeg,
        double endAngleDeg)
    {
        var geo = new StreamGeometry();
        double span = Math.Clamp(endAngleDeg - startAngleDeg, 0.1, 359.8);
        endAngleDeg = startAngleDeg + span;
        bool isLargeArc = span > 180.0;

        var p1 = PolarToCartesian(center, radius, startAngleDeg);
        var p2 = PolarToCartesian(center, radius, endAngleDeg);

        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(p1, isFilled: false, isClosed: false);
            ctx.ArcTo(p2, new System.Windows.Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        geo.Freeze();
        return geo;
    }

    private static System.Windows.Point PolarToCartesian(System.Windows.Point center, double radius, double angleDeg)
    {
        double rad = angleDeg * Math.PI / 180.0;
        return new System.Windows.Point(
            center.X + radius * Math.Cos(rad),
            center.Y + radius * Math.Sin(rad));
    }

    private static double NormalizeAngleDeg(double deg)
    {
        double r = deg % 360.0;
        if (r < 0) r += 360.0;
        return r;
    }

    private static double ShortestAngleDiffDeg(double fromDeg, double toDeg)
    {
        double diff = (toDeg - fromDeg + 540.0) % 360.0 - 180.0;
        return diff;
    }

    private static System.Windows.Media.Color WithAlpha(System.Windows.Media.Color c, byte alpha)
    {
        return System.Windows.Media.Color.FromArgb(alpha, c.R, c.G, c.B);
    }

    private FormattedText CreateTitleText(string text, Typeface typeface, double size, System.Windows.Media.Color color, double pixelsPerDip)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            size,
            new SolidColorBrush(color),
            pixelsPerDip);
    }

    /// <summary>
    /// Returns true when a horizontally centered text row (centerX, rowY, width, height) fits entirely inside
    /// the annular wedge [startAngle, endAngle] with a small padding from the radial dividers and the edges.
    /// </summary>
    private static bool IsRowInsideSlice(
        System.Windows.Point center,
        double centerX,
        double rowY,
        double width,
        double height,
        double startAngle,
        double endAngle,
        double maxTextRadius)
    {
        const double sidePadding = 6.0;
        const double radialPadding = 3.0;
        double halfW = width / 2.0;
        double halfH = height / 2.0;
        double midAngle = (startAngle + endAngle) / 2.0;
        double halfSpan = (endAngle - startAngle) / 2.0;

        foreach (var (px, py) in new[]
        {
            (centerX - halfW, rowY - halfH),
            (centerX + halfW, rowY - halfH),
            (centerX - halfW, rowY + halfH),
            (centerX + halfW, rowY + halfH)
        })
        {
            double dx = px - center.X;
            double dy = py - center.Y;
            double r = Math.Sqrt(dx * dx + dy * dy);
            if (r < InnerR + radialPadding || r > maxTextRadius - radialPadding) return false;

            double a = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            double diff = Math.Abs(ShortestAngleDiffDeg(midAngle, a));
            if (diff > halfSpan) return false;

            // Perpendicular distance from the nearer radial divider line
            double distToEdge = r * Math.Sin((halfSpan - diff) * Math.PI / 180.0);
            if (distToEdge < sidePadding) return false;
        }
        return true;
    }

    private static string TruncateText(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLen) return text;
        return text[..(maxLen - 1)] + "…";
    }

    private void DrawCenteredText(
        DrawingContext dc,
        string text,
        System.Windows.Point center,
        double fontSize,
        Typeface typeface,
        System.Windows.Media.Brush foreground)
    {
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            fontSize,
            foreground,
            pixelsPerDip);

        dc.DrawText(ft, new System.Windows.Point(center.X - ft.Width / 2.0, center.Y - ft.Height / 2.0));
    }
}
