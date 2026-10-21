using System;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VolumePie.Windows;

namespace VolumePie.Services;

public class BrightnessService : IDisposable
{
    private readonly DimmerOverlayManager _dimmerOverlay = new();
    private double _cachedBrightness = 100.0;
    private bool _hasInitialRead;
    private int _isRefreshing;
    private string _backendName = "画面輝度";

    private int _pendingTargetBrightness = -1;
    private int _isWorkerRunning;

    public bool EnableSoftwareFallback { get; set; } = true;

    public string BackendDescription => _backendName;

    public double GetBrightness()
    {
        if (!_hasInitialRead)
        {
            RefreshBrightnessAsync();
        }
        return _cachedBrightness;
    }

    public void RefreshBrightnessAsync()
    {
        if (Interlocked.CompareExchange(ref _isRefreshing, 1, 0) != 0)
            return;

        Task.Run(() =>
        {
            try
            {
                RefreshBrightnessCore();
            }
            finally
            {
                Interlocked.Exchange(ref _isRefreshing, 0);
            }
        });
    }

    private void RefreshBrightnessCore()
    {
        // 1. Try WMI (Internal Laptop Display)
        if (TryGetWmiBrightness(out int wmiVal))
        {
            _cachedBrightness = wmiVal;
            _backendName = "内蔵ディスプレイ (WMI)";
            _hasInitialRead = true;
            return;
        }

        // 2. Try DDC/CI on current cursor monitor
        if (TryGetDdcBrightness(out int ddcVal))
        {
            _cachedBrightness = ddcVal;
            _backendName = "外部モニター (DDC/CI)";
            _hasInitialRead = true;
            return;
        }

        // 3. Fallback to software dimmer
        _cachedBrightness = _dimmerOverlay.CurrentBrightness;
        _backendName = "画面調光 (ソフトウェア)";
        _hasInitialRead = true;
    }

    public void SetBrightness(double percent)
    {
        double clamped = Math.Clamp(percent, 0.0, 100.0);
        _cachedBrightness = clamped;
        _hasInitialRead = true;

        int targetInt = (int)Math.Round(clamped);
        Interlocked.Exchange(ref _pendingTargetBrightness, targetInt);

        if (Interlocked.CompareExchange(ref _isWorkerRunning, 1, 0) == 0)
        {
            Task.Run(ProcessBrightnessQueue);
        }
    }

    private void ProcessBrightnessQueue()
    {
        try
        {
            while (true)
            {
                int target = Interlocked.Exchange(ref _pendingTargetBrightness, -1);
                if (target < 0)
                    break;

                bool hardwareApplied = false;

                if (TrySetWmiBrightness(target))
                {
                    _backendName = "内蔵ディスプレイ (WMI)";
                    hardwareApplied = true;
                }
                else if (TrySetDdcBrightness(target))
                {
                    _backendName = "外部モニター (DDC/CI)";
                    hardwareApplied = true;
                }

                if (!hardwareApplied && EnableSoftwareFallback)
                {
                    _backendName = "画面調光 (ソフトウェア)";
                    _dimmerOverlay.SetBrightness(target);
                }
                else if (hardwareApplied)
                {
                    _dimmerOverlay.HideAll();
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isWorkerRunning, 0);
            if ( Volatile.Read(ref _pendingTargetBrightness) >= 0 &&
                 Interlocked.CompareExchange(ref _isWorkerRunning, 1, 0) == 0 )
            {
                Task.Run(ProcessBrightnessQueue);
            }
        }
    }

    private static bool TryGetWmiBrightness(out int brightness)
    {
        brightness = 100;
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            using var collection = searcher.Get();
            foreach (ManagementObject obj in collection)
            {
                using (obj)
                {
                    brightness = Convert.ToInt32(obj["CurrentBrightness"]);
                    return true;
                }
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool TrySetWmiBrightness(int brightness)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
            using var collection = searcher.Get();
            foreach (ManagementObject obj in collection)
            {
                using (obj)
                {
                    obj.InvokeMethod("WmiSetBrightness", [1u, (byte)Math.Clamp(brightness, 0, 100)]);
                    return true;
                }
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool TryGetDdcBrightness(out int brightness)
    {
        brightness = 100;
        try
        {
            GetCursorPos(out POINT pt);
            IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero) return false;

            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
                return false;

            var monitors = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors))
                return false;

            try
            {
                for (int i = 0; i < count; i++)
                {
                    if (GetMonitorBrightness(monitors[i].hPhysicalMonitor, out uint min, out uint cur, out uint max) && max > min)
                    {
                        brightness = (int)Math.Round((double)(cur - min) / (max - min) * 100.0);
                        return true;
                    }
                }
            }
            finally
            {
                DestroyPhysicalMonitors(count, monitors);
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool TrySetDdcBrightness(int brightnessPercent)
    {
        try
        {
            GetCursorPos(out POINT pt);
            IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero) return false;

            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
                return false;

            var monitors = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors))
                return false;

            bool anySuccess = false;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    if (GetMonitorBrightness(monitors[i].hPhysicalMonitor, out uint min, out _, out uint max) && max > min)
                    {
                        uint target = min + (uint)Math.Round(Math.Clamp(brightnessPercent, 0, 100) / 100.0 * (max - min));
                        if (SetMonitorBrightness(monitors[i].hPhysicalMonitor, target))
                        {
                            anySuccess = true;
                        }
                    }
                }
            }
            finally
            {
                DestroyPhysicalMonitors(count, monitors);
            }
            return anySuccess;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _dimmerOverlay.Dispose();
    }

    #region Win32 DDC/CI Interop

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

    #endregion
}
