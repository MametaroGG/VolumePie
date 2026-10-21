using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using VolumePie.Models;

namespace VolumePie.Services;

public class GlobalInputHookService : IDisposable
{
    private IntPtr _keyboardHookId = IntPtr.Zero;
    private IntPtr _mouseHookId = IntPtr.Zero;

    // Keep delegate references alive to prevent GC collection
    private readonly LowLevelProc _keyboardProc;
    private readonly LowLevelProc _mouseProc;

    private AppSettings _settings;
    private readonly DispatcherTimer _holdTimer;

    private bool _isHotkeyHeld;
    private bool _isMouseTriggerPending;
    private bool _isMouseTriggerActive;
    private bool _isSwallowingShiftRightClick;
    private bool _isSwallowingHoldButton;
    private MouseTriggerType _swallowedHoldTrigger = MouseTriggerType.None;
    private POINT _mouseDownPoint;

    public bool IsSuppressed { get; set; }

    public event Action<bool>? TriggerActivated;   // bool isMouseTrigger
    public event Action<bool>? TriggerReleased;    // bool isMouseTrigger
    public event Action<int, int>? GlobalMouseDown; // screen X, screen Y

    public GlobalInputHookService(AppSettings initialSettings)
    {
        _settings = initialSettings;
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;

        _holdTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Clamp(_settings.MouseHoldDelayMs, 80, 1000))
        };
        _holdTimer.Tick += OnHoldTimerTick;
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        _holdTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(_settings.MouseHoldDelayMs, 80, 1000));
    }

    public void Start()
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        IntPtr hMod = curModule != null ? GetModuleHandle(curModule.ModuleName) : IntPtr.Zero;

        if (_keyboardHookId == IntPtr.Zero)
        {
            _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
        }

        if (_mouseHookId == IntPtr.Zero)
        {
            _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
        }
    }

    public void Stop()
    {
        _holdTimer.Stop();
        if (_keyboardHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = IntPtr.Zero;
        }

        if (_mouseHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
        }
    }

    private void OnHoldTimerTick(object? sender, EventArgs e)
    {
        _holdTimer.Stop();
        if (_isMouseTriggerPending && !IsSuppressed)
        {
            _isMouseTriggerPending = false;
            _isMouseTriggerActive = true;
            TriggerActivated?.Invoke(true);
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !IsSuppressed && _settings.EnableKeyboardShortcut && _settings.HotkeyVirtualKey > 0)
        {
            int msg = wParam.ToInt32();
            int vkCode = Marshal.ReadInt32(lParam);

            bool isKeyDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            bool isKeyUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

            if (isKeyDown && vkCode == _settings.HotkeyVirtualKey)
            {
                if (AreConfiguredModifiersPressed(_settings.HotkeyModifiers))
                {
                    if (!_isHotkeyHeld)
                    {
                        _isHotkeyHeld = true;
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            TriggerActivated?.Invoke(false);
                        });
                    }
                    if (!_settings.PassThroughKeyboardShortcut)
                    {
                        // Suppress key event so it doesn't type into active window
                        return (IntPtr)1;
                    }
                }
            }
            else if (isKeyUp && _isHotkeyHeld)
            {
                if (vkCode == _settings.HotkeyVirtualKey || IsModifierVirtualKey(vkCode, _settings.HotkeyModifiers))
                {
                    _isHotkeyHeld = false;
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        TriggerReleased?.Invoke(false);
                    });
                }
            }
        }

        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            // Let synthetic clicks replayed by VolumePie pass straight through to the OS and target app
            if ((hookStruct.flags & LLKHF_INJECTED) != 0)
            {
                return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
            }

            // Always complete any in-progress swallowed button pair on ButtonUp
            if (msg == WM_RBUTTONUP && _isSwallowingShiftRightClick)
            {
                _isSwallowingShiftRightClick = false;
                if (_isMouseTriggerActive)
                {
                    _isMouseTriggerActive = false;
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        TriggerReleased?.Invoke(true);
                    });
                }
                return (IntPtr)1;
            }

            if (_isSwallowingHoldButton)
            {
                if (msg == WM_MBUTTONUP && _swallowedHoldTrigger == MouseTriggerType.MiddleButtonHold)
                {
                    _isSwallowingHoldButton = false;
                    _swallowedHoldTrigger = MouseTriggerType.None;
                    bool wasShortClick = _isMouseTriggerPending && !_isMouseTriggerActive;
                    EndMouseHold();
                    if (wasShortClick)
                    {
                        System.Threading.Tasks.Task.Run(() => ReplayShortMouseClick(MouseTriggerType.MiddleButtonHold));
                    }
                    return (IntPtr)1;
                }

                if (msg == WM_XBUTTONUP &&
                    (_swallowedHoldTrigger == MouseTriggerType.XButton1Hold || _swallowedHoldTrigger == MouseTriggerType.XButton2Hold))
                {
                    int expectedXButton = _swallowedHoldTrigger == MouseTriggerType.XButton1Hold ? XBUTTON1 : XBUTTON2;
                    int xButton = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                    if (xButton == expectedXButton)
                    {
                        MouseTriggerType triggerToReplay = _swallowedHoldTrigger;
                        _isSwallowingHoldButton = false;
                        _swallowedHoldTrigger = MouseTriggerType.None;
                        bool wasShortClick = _isMouseTriggerPending && !_isMouseTriggerActive;
                        EndMouseHold();
                        if (wasShortClick)
                        {
                            System.Threading.Tasks.Task.Run(() => ReplayShortMouseClick(triggerToReplay));
                        }
                        return (IntPtr)1;
                    }
                }
            }

            if (!IsSuppressed)
            {
                // Handle Shift + Right-Click as a matched (WM_RBUTTONDOWN + WM_RBUTTONUP) pair so Windows never sees the right-click at all
                if (_settings.MouseTrigger == MouseTriggerType.ShiftPlusRightClick)
                {
                    if (msg == WM_RBUTTONDOWN && (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0)
                    {
                        _isSwallowingShiftRightClick = true;
                        _isMouseTriggerActive = true;
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            TriggerActivated?.Invoke(true);
                        });
                        return (IntPtr)1;
                    }
                }

                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN)
                {
                    int clickX = hookStruct.pt.X;
                    int clickY = hookStruct.pt.Y;
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        GlobalMouseDown?.Invoke(clickX, clickY);
                    });
                }

                if (_settings.MouseTrigger != MouseTriggerType.None)
                {
                    switch (_settings.MouseTrigger)
                    {
                        case MouseTriggerType.MiddleButtonHold:
                            if (msg == WM_MBUTTONDOWN)
                            {
                                _isSwallowingHoldButton = true;
                                _swallowedHoldTrigger = MouseTriggerType.MiddleButtonHold;
                                BeginMouseHold(hookStruct.pt);
                                return (IntPtr)1;
                            }
                            break;

                        case MouseTriggerType.XButton1Hold:
                        case MouseTriggerType.XButton2Hold:
                            int targetXButton = _settings.MouseTrigger == MouseTriggerType.XButton1Hold ? XBUTTON1 : XBUTTON2;
                            int xButton = (short)((hookStruct.mouseData >> 16) & 0xFFFF);

                            if (msg == WM_XBUTTONDOWN && xButton == targetXButton)
                            {
                                _isSwallowingHoldButton = true;
                                _swallowedHoldTrigger = _settings.MouseTrigger;
                                BeginMouseHold(hookStruct.pt);
                                return (IntPtr)1;
                            }
                            break;
                    }

                    // If user holds the trigger button and immediately starts dragging (> 16px), activate without waiting for full timer
                    if (_isMouseTriggerPending && msg == WM_MOUSEMOVE)
                    {
                        int dx = hookStruct.pt.X - _mouseDownPoint.X;
                        int dy = hookStruct.pt.Y - _mouseDownPoint.Y;
                        if ((dx * dx + dy * dy) > 256)
                        {
                            _holdTimer.Stop();
                            _isMouseTriggerPending = false;
                            _isMouseTriggerActive = true;
                            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                            {
                                TriggerActivated?.Invoke(true);
                            });
                        }
                    }
                }
            }
        }

        // CRITICAL: Always pass all non-swallowed mouse events to CallNextHookEx so ButtonDown/ButtonUp states always match
        return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    private static void ReplayShortMouseClick(MouseTriggerType trigger)
    {
        switch (trigger)
        {
            case MouseTriggerType.MiddleButtonHold:
                mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, UIntPtr.Zero);
                break;
            case MouseTriggerType.XButton1Hold:
                mouse_event(MOUSEEVENTF_XDOWN, 0, 0, (uint)XBUTTON1, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_XUP, 0, 0, (uint)XBUTTON1, UIntPtr.Zero);
                break;
            case MouseTriggerType.XButton2Hold:
                mouse_event(MOUSEEVENTF_XDOWN, 0, 0, (uint)XBUTTON2, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_XUP, 0, 0, (uint)XBUTTON2, UIntPtr.Zero);
                break;
        }
    }

    private void BeginMouseHold(POINT pt)
    {
        _mouseDownPoint = pt;
        _isMouseTriggerPending = true;
        _isMouseTriggerActive = false;
        _holdTimer.Stop();
        _holdTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(_settings.MouseHoldDelayMs, 80, 1000));
        _holdTimer.Start();
    }

    private bool EndMouseHold()
    {
        _holdTimer.Stop();
        _isMouseTriggerPending = false;

        if (_isMouseTriggerActive)
        {
            _isMouseTriggerActive = false;
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                TriggerReleased?.Invoke(true);
            });
            return true;
        }
        return false;
    }

    private static bool AreConfiguredModifiersPressed(uint requiredModifiers)
    {
        bool altPressed = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        bool ctrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        bool shiftPressed = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        bool winPressed = ((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0) || ((GetAsyncKeyState(VK_RWIN) & 0x8000) != 0);

        bool needAlt = (requiredModifiers & 1) != 0;
        bool needCtrl = (requiredModifiers & 2) != 0;
        bool needShift = (requiredModifiers & 4) != 0;
        bool needWin = (requiredModifiers & 8) != 0;

        return altPressed == needAlt &&
               ctrlPressed == needCtrl &&
               shiftPressed == needShift &&
               winPressed == needWin;
    }

    private static bool IsModifierVirtualKey(int vk, uint configuredModifiers)
    {
        if ((configuredModifiers & 1) != 0 && (vk == VK_MENU || vk == VK_LMENU || vk == VK_RMENU)) return true;
        if ((configuredModifiers & 2) != 0 && (vk == VK_CONTROL || vk == VK_LCONTROL || vk == VK_RCONTROL)) return true;
        if ((configuredModifiers & 4) != 0 && (vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT)) return true;
        if ((configuredModifiers & 8) != 0 && (vk == VK_LWIN || vk == VK_RWIN)) return true;
        return false;
    }

    public void Dispose()
    {
        Stop();
    }

    #region Win32 Hooks Interop

    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WM_XBUTTONUP = 0x020C;
    private const int WM_CANCELMODE = 0x001F;

    private const int XBUTTON1 = 1;
    private const int XBUTTON2 = 2;

    private const uint LLKHF_INJECTED = 0x00000001;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_XDOWN = 0x0080;
    private const uint MOUSEEVENTF_XUP = 0x0100;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;

    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    #endregion
}
