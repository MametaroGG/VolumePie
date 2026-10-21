using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace VolumePie.Windows;

/// <summary>
/// Click-through screen dimmer overlay used when hardware WMI / DDC-CI brightness control is unavailable.
/// Enforces WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE after Window.Show() and hooks WM_NCHITTEST
/// so it can never intercept mouse clicks.
/// </summary>
public class DimmerOverlayManager : IDisposable
{
    private readonly List<Window> _overlayWindows = new();
    private double _currentBrightness = 100.0;

    public double CurrentBrightness => _currentBrightness;

    public void SetBrightness(double brightnessPercent)
    {
        _currentBrightness = Math.Clamp(brightnessPercent, 10.0, 100.0);

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (_currentBrightness >= 99.0)
            {
                HideAll();
                return;
            }

            EnsureWindowsCreated();

            // Map 10%..100% brightness -> 0.75..0.0 black overlay opacity
            double normalized = (_currentBrightness - 10.0) / 90.0;
            double opacity = Math.Clamp((1.0 - normalized) * 0.75, 0.0, 0.75);

            foreach (var win in _overlayWindows)
            {
                if (!win.IsVisible)
                {
                    win.Show();
                }

                win.Opacity = opacity;

                // Always re-apply click-through extended style AFTER Show() and Opacity changes,
                // because WPF's internal Window lifecycle can overwrite GWL_EXSTYLE during Show().
                if (!EnforceClickThroughStyle(win))
                {
                    // Safety fallback: never leave a window visible if click-through style failed
                    win.Hide();
                }
            }
        });
    }

    private void EnsureWindowsCreated()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (_overlayWindows.Count == screens.Length)
            return;

        CloseAll();

        foreach (var screen in screens)
        {
            var win = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Black,
                Opacity = 0.0,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = screen.Bounds.Left,
                Top = screen.Bounds.Top,
                Width = screen.Bounds.Width,
                Height = screen.Bounds.Height,
                IsHitTestVisible = false,
                Focusable = false,
                IsEnabled = false
            };

            win.SourceInitialized += (_, _) =>
            {
                IntPtr hwnd = new WindowInteropHelper(win).Handle;
                var source = HwndSource.FromHwnd(hwnd);
                source?.AddHook(ClickThroughWndProc);
                EnforceClickThroughStyle(win);
            };

            win.Loaded += (_, _) => EnforceClickThroughStyle(win);

            _overlayWindows.Add(win);
        }
    }

    private static bool EnforceClickThroughStyle(Window win)
    {
        IntPtr hwnd = new WindowInteropHelper(win).Handle;
        if (hwnd == IntPtr.Zero)
            return false;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        int desired = exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        if (exStyle != desired)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, desired);
        }

        int verify = GetWindowLong(hwnd, GWL_EXSTYLE);
        return (verify & WS_EX_TRANSPARENT) != 0 && (verify & WS_EX_LAYERED) != 0;
    }

    private static IntPtr ClickThroughWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
        {
            handled = true;
            return new IntPtr(HTTRANSPARENT);
        }
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(MA_NOACTIVATE);
        }
        return IntPtr.Zero;
    }

    public void HideAll()
    {
        foreach (var win in _overlayWindows)
        {
            if (win.IsVisible)
                win.Hide();
        }
    }

    private void CloseAll()
    {
        foreach (var win in _overlayWindows)
        {
            try { win.Close(); } catch { }
        }
        _overlayWindows.Clear();
    }

    public void Dispose()
    {
        CloseAll();
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int WM_NCHITTEST = 0x0084;
    private const int HTTRANSPARENT = -1;
    private const int MA_NOACTIVATE = 3;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
}
