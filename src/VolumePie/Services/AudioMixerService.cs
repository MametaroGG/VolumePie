using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VolumePie.Models;

namespace VolumePie.Services;

public sealed class AudioOutputDeviceInfo
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public bool IsHeadphones =>
        Name.Contains("ヘッドホン", StringComparison.OrdinalIgnoreCase) ||
        Name.Contains("ヘッドセット", StringComparison.OrdinalIgnoreCase) ||
        Name.Contains("Headphone", StringComparison.OrdinalIgnoreCase) ||
        Name.Contains("Headset", StringComparison.OrdinalIgnoreCase) ||
        Name.Contains("Earphone", StringComparison.OrdinalIgnoreCase);
}

public class AudioMixerService
{
    private static readonly ConcurrentDictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    public List<AudioOutputDeviceInfo> GetOutputDevices()
    {
        var list = new List<AudioOutputDeviceInfo>();
        try
        {
            var enumerator = CreateEnumerator();
            if (enumerator == null) return list;

            string defaultId = string.Empty;
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var defDev) == 0 && defDev != null)
            {
                defDev.GetId(out defaultId);
            }

            if (enumerator.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATE_ACTIVE, out var collection) != 0 || collection == null)
                return list;

            if (collection.GetCount(out uint count) != 0 || count == 0)
                return list;

            var pkeyFriendlyName = new PROPERTYKEY
            {
                fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
                pid = 14
            };

            for (uint i = 0; i < count; i++)
            {
                if (collection.Item(i, out var dev) != 0 || dev == null)
                    continue;

                if (dev.GetId(out string id) != 0 || string.IsNullOrEmpty(id))
                    continue;

                string name = id;
                if (dev.OpenPropertyStore(STGM_READ, out var store) == 0 && store != null)
                {
                    if (store.GetValue(ref pkeyFriendlyName, out var pv) == 0 && pv.pwszVal != IntPtr.Zero)
                    {
                        name = Marshal.PtrToStringUni(pv.pwszVal) ?? id;
                    }
                }

                list.Add(new AudioOutputDeviceInfo
                {
                    Id = id,
                    Name = name,
                    IsDefault = string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase)
                });
            }
        }
        catch
        {
        }
        return list;
    }

    public bool SetDefaultOutputDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return false;
        try
        {
            Type? type = Type.GetTypeFromCLSID(new Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9"));
            if (type == null) return false;
            if (Activator.CreateInstance(type) is not IPolicyConfig policyConfig)
                return false;

            int hr0 = policyConfig.SetDefaultEndpoint(deviceId, (int)ERole.eConsole);
            int hr1 = policyConfig.SetDefaultEndpoint(deviceId, (int)ERole.eMultimedia);
            int hr2 = policyConfig.SetDefaultEndpoint(deviceId, (int)ERole.eCommunications);
            return (hr0 == 0 || hr1 == 0 || hr2 == 0);
        }
        catch
        {
            return false;
        }
    }

    public (double Volume, bool IsMuted, bool Available) GetMasterOutputState()
    {
        return GetEndpointState(EDataFlow.eRender);
    }

    public void SetMasterOutputVolume(double volumePercent)
    {
        SetEndpointVolume(EDataFlow.eRender, volumePercent);
    }

    public bool ToggleMasterOutputMute()
    {
        return ToggleEndpointMute(EDataFlow.eRender);
    }

    public (double Volume, bool IsMuted, bool Available) GetMicInputState()
    {
        return GetEndpointState(EDataFlow.eCapture);
    }

    public void SetMicInputVolume(double volumePercent)
    {
        SetEndpointVolume(EDataFlow.eCapture, volumePercent);
    }

    public bool ToggleMicInputMute()
    {
        return ToggleEndpointMute(EDataFlow.eCapture);
    }

    public List<PieItem> GetAppSessionItems(int maxCount = 6)
    {
        var result = new List<PieItem>();
        try
        {
            var enumerator = CreateEnumerator();
            if (enumerator == null) return result;

            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device) != 0 || device == null)
                return result;

            Guid iidSessionMgr2 = typeof(IAudioSessionManager2).GUID;
            if (device.Activate(ref iidSessionMgr2, CLSCTX_ALL, IntPtr.Zero, out object mgrObj) != 0 || mgrObj is not IAudioSessionManager2 mgr)
                return result;

            if (mgr.GetSessionEnumerator(out var sessionEnum) != 0 || sessionEnum == null)
                return result;

            if (sessionEnum.GetCount(out int count) != 0 || count <= 0)
                return result;

            uint currentPid = (uint)Environment.ProcessId;
            var parentMap = BuildProcessParentMap();

            // Group by normalized ProcessName (case-insensitive) so multi-process apps (e.g. LineMediaPlayer, Chrome, Discord) never duplicate!
            var groupedByApp = new Dictionary<string, AppSessionGroup>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < count; i++)
            {
                if (sessionEnum.GetSession(i, out var ctl2) != 0 || ctl2 == null)
                    continue;

                if (ctl2.GetState(out int state) != 0)
                    continue;

                // 2 == AudioSessionStateExpired
                if (state == 2)
                    continue;

                bool isSystemSounds = ctl2.IsSystemSoundsSession() == 0; // S_OK == 0 means true
                if (isSystemSounds)
                {
                    if (ctl2 is ISimpleAudioVolume sysVol)
                    {
                        sysVol.GetMasterVolume(out float sysScalar);
                        sysVol.GetMute(out bool sysMuted);

                        string sysGroupKey = "SystemSounds";
                        if (!groupedByApp.ContainsKey(sysGroupKey))
                        {
                            ctl2.GetIconPath(out string? sysIconPath);
                            ImageSource? sysIcon = null;
                            if (!string.IsNullOrWhiteSpace(sysIconPath))
                            {
                                sysIcon = IconCache.GetOrAdd("session:" + sysIconPath, _ => ExtractIconFromResourcePath(sysIconPath));
                            }

                            groupedByApp[sysGroupKey] = new AppSessionGroup
                            {
                                ProcessName = "SystemSounds",
                                Title = "システム音",
                                Icon = sysIcon,
                                IsActivePlaying = (state == 1),
                                VolumePercent = Math.Round(sysScalar * 100.0, 1),
                                IsMuted = sysMuted
                            };
                        }
                    }
                    continue;
                }

                if (ctl2.GetProcessId(out uint pid) != 0 || pid == 0 || pid == currentPid)
                    continue;

                if (ctl2 is not ISimpleAudioVolume simpleVol)
                    continue;

                simpleVol.GetMasterVolume(out float volScalar);
                simpleVol.GetMute(out bool muted);

                ctl2.GetDisplayName(out string? displayName);
                ctl2.GetIconPath(out string? sessionIconPath);

                uint effectivePid = ResolveHostProcessId(pid, parentMap);
                var procInfo = ResolveProcessInfo(effectivePid, displayName, sessionIconPath)
                               ?? ResolveProcessInfo(pid, displayName, sessionIconPath);
                if (procInfo == null)
                    continue;

                // Skip internal helper / background subprocesses (e.g. msedgewebview2, crashpad_handler) that Windows Volume Mixer hides
                if (IsIgnoredSubprocess(procInfo.Value.ProcessName) ||
                    (!string.IsNullOrEmpty(procInfo.Value.ExePath) && IsIgnoredSubprocess(Path.GetFileNameWithoutExtension(procInfo.Value.ExePath))))
                {
                    continue;
                }

                // Skip idle system background hosts (e.g. SearchHost, Widgets, Explorer) when they are not actively playing sound
                if (IsSystemBackgroundHost(procInfo.Value.ProcessName) && state != 1)
                {
                    continue;
                }

                string groupKey = !string.IsNullOrWhiteSpace(procInfo.Value.ExePath)
                    ? procInfo.Value.ExePath
                    : procInfo.Value.ProcessName;

                if (!groupedByApp.TryGetValue(groupKey, out var group))
                {
                    group = new AppSessionGroup
                    {
                        ProcessName = procInfo.Value.ProcessName,
                        Title = procInfo.Value.Title,
                        Icon = procInfo.Value.Icon,
                        IsActivePlaying = (state == 1),
                        VolumePercent = Math.Round(volScalar * 100.0, 1),
                        IsMuted = muted
                    };
                    group.ProcessIds.Add(pid);
                    if (effectivePid != pid)
                    {
                        group.ProcessIds.Add(effectivePid);
                    }
                    groupedByApp[groupKey] = group;
                }
                else
                {
                    group.ProcessIds.Add(pid);
                    if (effectivePid != pid)
                    {
                        group.ProcessIds.Add(effectivePid);
                    }
                    if (state == 1 && !group.IsActivePlaying)
                    {
                        // Prefer the volume/mute state of the actively playing process instance
                        group.IsActivePlaying = true;
                        group.VolumePercent = Math.Round(volScalar * 100.0, 1);
                        group.IsMuted = muted;
                    }
                    if (group.Icon == null && procInfo.Value.Icon != null)
                    {
                        group.Icon = procInfo.Value.Icon;
                    }
                }
            }

            var orderedGroups = groupedByApp.Values
                .OrderByDescending(g => g.IsActivePlaying)
                .ThenByDescending(g => g.ProcessName.Equals("SystemSounds", StringComparison.OrdinalIgnoreCase))
                .ThenBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase)
                .Take(maxCount)
                .ToList();

            foreach (var g in orderedGroups)
            {
                var targetPids = new HashSet<uint>(g.ProcessIds);
                string targetProcName = g.ProcessName;
                bool isSysSounds = targetProcName.Equals("SystemSounds", StringComparison.OrdinalIgnoreCase);

                result.Add(new PieItem
                {
                    Id = $"app_{targetProcName}",
                    Title = isSysSounds ? "システム音" : g.Title,
                    Subtitle = isSysSounds ? "Windows システム音" : (g.IsActivePlaying ? "再生中アプリ" : "アプリ音量"),
                    Category = isSysSounds ? PieItemCategory.SystemSounds : PieItemCategory.AppAudio,
                    Value = g.VolumePercent,
                    IsActivePlaying = g.IsActivePlaying,
                    IsMuted = g.IsMuted,
                    CanMute = true,
                    AppIcon = g.Icon,
                    AccentColor = isSysSounds ? System.Windows.Media.Color.FromRgb(245, 158, 11) : System.Windows.Media.Color.FromRgb(56, 189, 248),
                    OnValueChanged = newVol => SetAppGroupAudioVolume(targetProcName, targetPids, newVol),
                    OnToggleMute = () => ToggleAppGroupAudioMute(targetProcName, targetPids)
                });
            }
        }
        catch
        {
            // Ignore transient COM enumeration errors
        }

        return result;
    }

    private void SetAppGroupAudioVolume(string targetProcName, HashSet<uint> targetPids, double volumePercent)
    {
        try
        {
            var enumerator = CreateEnumerator();
            if (enumerator == null) return;
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device) != 0 || device == null)
                return;

            Guid iidSessionMgr2 = typeof(IAudioSessionManager2).GUID;
            if (device.Activate(ref iidSessionMgr2, CLSCTX_ALL, IntPtr.Zero, out object mgrObj) != 0 || mgrObj is not IAudioSessionManager2 mgr)
                return;

            if (mgr.GetSessionEnumerator(out var sessionEnum) != 0 || sessionEnum == null)
                return;

            sessionEnum.GetCount(out int count);
            float scalar = (float)Math.Clamp(volumePercent / 100.0, 0.0, 1.0);
            Guid ctx = Guid.Empty;
            bool isSystemSounds = targetProcName.Equals("SystemSounds", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < count; i++)
            {
                if (sessionEnum.GetSession(i, out var ctl2) == 0 && ctl2 != null)
                {
                    if (isSystemSounds)
                    {
                        if (ctl2.IsSystemSoundsSession() == 0 && ctl2 is ISimpleAudioVolume simpleVol)
                        {
                            simpleVol.SetMasterVolume(scalar, ref ctx);
                        }
                    }
                    else
                    {
                        if (ctl2.GetProcessId(out uint pid) == 0 && pid != 0 && ctl2 is ISimpleAudioVolume simpleVol)
                        {
                            if (MatchesAppGroup(pid, targetProcName, targetPids))
                            {
                                simpleVol.SetMasterVolume(scalar, ref ctx);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
        }
    }

    private bool ToggleAppGroupAudioMute(string targetProcName, HashSet<uint> targetPids)
    {
        bool newMuteState = false;
        try
        {
            var enumerator = CreateEnumerator();
            if (enumerator == null) return false;
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device) != 0 || device == null)
                return false;

            Guid iidSessionMgr2 = typeof(IAudioSessionManager2).GUID;
            if (device.Activate(ref iidSessionMgr2, CLSCTX_ALL, IntPtr.Zero, out object mgrObj) != 0 || mgrObj is not IAudioSessionManager2 mgr)
                return false;

            if (mgr.GetSessionEnumerator(out var sessionEnum) != 0 || sessionEnum == null)
                return false;

            sessionEnum.GetCount(out int count);
            Guid ctx = Guid.Empty;
            bool determined = false;
            bool isSystemSounds = targetProcName.Equals("SystemSounds", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < count; i++)
            {
                if (sessionEnum.GetSession(i, out var ctl2) == 0 && ctl2 != null)
                {
                    if (isSystemSounds)
                    {
                        if (ctl2.IsSystemSoundsSession() == 0 && ctl2 is ISimpleAudioVolume simpleVol)
                        {
                            if (!determined)
                            {
                                simpleVol.GetMute(out bool currentMute);
                                newMuteState = !currentMute;
                                determined = true;
                            }
                            simpleVol.SetMute(newMuteState, ref ctx);
                        }
                    }
                    else
                    {
                        if (ctl2.GetProcessId(out uint pid) == 0 && pid != 0 && ctl2 is ISimpleAudioVolume simpleVol)
                        {
                            if (MatchesAppGroup(pid, targetProcName, targetPids))
                            {
                                if (!determined)
                                {
                                    simpleVol.GetMute(out bool currentMute);
                                    newMuteState = !currentMute;
                                    determined = true;
                                }
                                simpleVol.SetMute(newMuteState, ref ctx);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
        }
        return newMuteState;
    }

    private static bool MatchesAppGroup(uint pid, string targetProcName, HashSet<uint> targetPids)
    {
        if (targetPids.Contains(pid))
            return true;

        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return string.Equals(proc.ProcessName, targetProcName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<uint, (uint ParentPid, string ExeName)> BuildProcessParentMap()
    {
        var map = new Dictionary<uint, (uint ParentPid, string ExeName)>();
        IntPtr snap = CreateToolhelp32Snapshot(2 /* TH32CS_SNAPPROCESS */, 0);
        if (snap != IntPtr.Zero && snap != (IntPtr)(-1))
        {
            try
            {
                var entry = new PROCESSENTRY32();
                entry.dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>();
                if (Process32First(snap, ref entry))
                {
                    do
                    {
                        map[entry.th32ProcessID] = (entry.th32ParentProcessID, entry.szExeFile);
                    } while (Process32Next(snap, ref entry));
                }
            }
            finally
            {
                CloseHandle(snap);
            }
        }
        return map;
    }

    private static bool IsHelperSubprocess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;

        string name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        // Note: LineMediaPlayer is intentionally NOT a helper subprocess; it has its own independent
        // volume slider and icon in the Windows Volume Mixer, so it remains a separate item.
        return name.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase)
            || name.Equals("QtWebEngineProcess", StringComparison.OrdinalIgnoreCase)
            || name.Equals("CefSharp.BrowserSubprocess", StringComparison.OrdinalIgnoreCase)
            || name.Equals("crashpad_handler", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnoredSubprocess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;

        string name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        return name.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase)
            || name.Equals("crashpad_handler", StringComparison.OrdinalIgnoreCase)
            || name.Equals("QtWebEngineProcess", StringComparison.OrdinalIgnoreCase)
            || name.Equals("CefSharp.BrowserSubprocess", StringComparison.OrdinalIgnoreCase)
            || name.Equals("werfault", StringComparison.OrdinalIgnoreCase)
            || name.Equals("audiodg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSystemBackgroundHost(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;

        string name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        return name.Equals("SearchHost", StringComparison.OrdinalIgnoreCase)
            || name.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Widgets", StringComparison.OrdinalIgnoreCase)
            || name.Equals("WidgetService", StringComparison.OrdinalIgnoreCase)
            || name.Equals("explorer", StringComparison.OrdinalIgnoreCase);
    }

    private static uint ResolveHostProcessId(uint pid, Dictionary<uint, (uint ParentPid, string ExeName)> parentMap)
    {
        uint current = pid;
        for (int depth = 0; depth < 6; depth++)
        {
            if (parentMap.TryGetValue(current, out var info) && info.ParentPid != 0 && info.ParentPid != current)
            {
                if (IsHelperSubprocess(info.ExeName))
                {
                    current = info.ParentPid;
                    continue;
                }
            }
            break;
        }
        return current;
    }

    private static (string Title, string ProcessName, string? ExePath, ImageSource? Icon)? ResolveProcessInfo(uint pid, string? sessionDisplayName, string? sessionIconPath)
    {
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            string procName = proc.ProcessName;
            string? exePath = GetProcessImagePath(proc);

            string title = !string.IsNullOrWhiteSpace(sessionDisplayName) && !sessionDisplayName.StartsWith('@')
                ? sessionDisplayName.Trim()
                : (!string.IsNullOrWhiteSpace(proc.MainWindowTitle) ? proc.MainWindowTitle.Trim() : string.Empty);

            if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrEmpty(exePath))
            {
                try
                {
                    var fvi = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(fvi.FileDescription) && fvi.FileDescription.Length <= 18)
                    {
                        title = fvi.FileDescription.Trim();
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = procName;
            }

            if (IsSystemBackgroundHost(title))
            {
                if (title.Equals("SearchHost", StringComparison.OrdinalIgnoreCase))
                    title = "Windows 検索";
                else if (title.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase))
                    title = "スタートメニュー";
                else if (title.Equals("Widgets", StringComparison.OrdinalIgnoreCase) || title.Equals("WidgetService", StringComparison.OrdinalIgnoreCase))
                    title = "ウィジェット";
                else
                    title = "Windows システム";
            }

            if (title.Length > 18)
            {
                int dashIdx = title.LastIndexOf(" - ", StringComparison.Ordinal);
                if (dashIdx > 0 && dashIdx + 3 < title.Length)
                    title = title[(dashIdx + 3)..].Trim();

                if (title.Length > 18)
                    title = procName;
            }

            ImageSource? icon = null;
            if (!string.IsNullOrEmpty(exePath))
            {
                icon = IconCache.GetOrAdd(exePath, ExtractFrozenIcon);
            }

            // Fallback: icon path explicitly published by the audio session (e.g. "C:\app.exe,-101" or "@%SystemRoot%\...")
            if (icon == null && !string.IsNullOrWhiteSpace(sessionIconPath))
            {
                icon = IconCache.GetOrAdd("session:" + sessionIconPath, _ => ExtractIconFromResourcePath(sessionIconPath));
            }

            // Fallback: protected/system processes (e.g. "StarDeskServer" of NetEase UU Remote) deny all access,
            // so borrow the icon of the same app's accessible GUI process or its installed uninstall entry.
            if (icon == null && string.IsNullOrEmpty(exePath))
            {
                string titleForLookup = title;
                icon = IconCache.GetOrAdd("fallback:" + procName, _ => ResolveFallbackAppIcon(procName, titleForLookup));
            }

            return (title, procName, exePath, icon);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves the full exe path of a process. Uses QueryFullProcessImageName with PROCESS_QUERY_LIMITED_INFORMATION,
    /// which also works for elevated (admin) / service processes where Process.MainModule throws "Access is denied".
    /// </summary>
    private static string? GetProcessImagePath(Process proc)
    {
        IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)proc.Id);
        if (hProcess != IntPtr.Zero)
        {
            try
            {
                var sb = new System.Text.StringBuilder(1024);
                int size = sb.Capacity;
                if (QueryFullProcessImageName(hProcess, 0, sb, ref size) && size > 0)
                {
                    return sb.ToString(0, size);
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }

        try
        {
            return proc.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? ExtractIconFromResourcePath(string resourcePath)
    {
        try
        {
            string path = resourcePath.Trim().TrimStart('@');
            int index = 0;
            int comma = path.LastIndexOf(',');
            if (comma > 0 && int.TryParse(path[(comma + 1)..].Trim(), out int parsed))
            {
                index = parsed;
                path = path[..comma];
            }
            path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (!File.Exists(path)) return null;

            var large = new IntPtr[1];
            if (ExtractIconEx(path, index, large, null, 1) > 0 && large[0] != IntPtr.Zero)
            {
                try
                {
                    var bmp = Imaging.CreateBitmapSourceFromHIcon(large[0], Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    bmp.Freeze();
                    return bmp;
                }
                finally
                {
                    DestroyIcon(large[0]);
                }
            }

            return ExtractFrozenIcon(path);
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? ResolveFallbackAppIcon(string procName, string title)
    {
        // 1. Accessible sibling process whose name is the longest prefix of this process name (StarDesk -> StarDeskServer)
        try
        {
            string? bestPath = null;
            int bestLen = 0;
            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    string name = p.ProcessName;
                    if (name.Length < 4 || name.Length <= bestLen) continue;
                    if (string.Equals(name, procName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!procName.StartsWith(name, StringComparison.OrdinalIgnoreCase)) continue;

                    string? path = GetProcessImagePath(p);
                    if (!string.IsNullOrEmpty(path))
                    {
                        bestPath = path;
                        bestLen = name.Length;
                    }
                }
            }

            if (bestPath != null)
            {
                var icon = IconCache.GetOrAdd(bestPath, ExtractFrozenIcon);
                if (icon != null) return icon;
            }
        }
        catch
        {
        }

        // 2. Installed app uninstall entry (DisplayIcon) matching the session title or process name prefix
        try
        {
            string[] roots =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            foreach (var hive in new[] { Microsoft.Win32.Registry.LocalMachine, Microsoft.Win32.Registry.CurrentUser })
            {
                foreach (var root in roots)
                {
                    using var rootKey = hive.OpenSubKey(root);
                    if (rootKey == null) continue;
                    foreach (var sub in rootKey.GetSubKeyNames())
                    {
                        using var key = rootKey.OpenSubKey(sub);
                        if (key?.GetValue("DisplayName") is not string displayName || string.IsNullOrWhiteSpace(displayName)) continue;
                        if (key.GetValue("DisplayIcon") is not string displayIcon || string.IsNullOrWhiteSpace(displayIcon)) continue;

                        string compact = displayName.Replace(" ", string.Empty);
                        bool matches = string.Equals(displayName, title, StringComparison.OrdinalIgnoreCase) ||
                                       (compact.Length >= 4 && procName.StartsWith(compact, StringComparison.OrdinalIgnoreCase));
                        if (!matches) continue;

                        var icon = ExtractIconFromResourcePath(displayIcon);
                        if (icon != null) return icon;
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, System.Text.StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static ImageSource? ExtractFrozenIcon(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
            if (sysIcon == null) return null;

            var bmpSource = Imaging.CreateBitmapSourceFromHIcon(
                sysIcon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));
            bmpSource.Freeze();
            return bmpSource;
        }
        catch
        {
            return null;
        }
    }

    private static (double Volume, bool IsMuted, bool Available) GetEndpointState(EDataFlow flow)
    {
        try
        {
            var epVol = GetEndpointVolumeInterface(flow);
            if (epVol == null) return (0, false, false);

            epVol.GetMasterVolumeLevelScalar(out float level);
            epVol.GetMute(out bool muted);
            return (Math.Round(level * 100.0, 1), muted, true);
        }
        catch
        {
            return (0, false, false);
        }
    }

    private static void SetEndpointVolume(EDataFlow flow, double volumePercent)
    {
        try
        {
            var epVol = GetEndpointVolumeInterface(flow);
            if (epVol == null) return;

            float scalar = (float)Math.Clamp(volumePercent / 100.0, 0.0, 1.0);
            Guid ctx = Guid.Empty;
            epVol.SetMasterVolumeLevelScalar(scalar, ref ctx);
        }
        catch
        {
        }
    }

    private static bool ToggleEndpointMute(EDataFlow flow)
    {
        try
        {
            var epVol = GetEndpointVolumeInterface(flow);
            if (epVol == null) return false;

            epVol.GetMute(out bool muted);
            bool next = !muted;
            Guid ctx = Guid.Empty;
            epVol.SetMute(next, ref ctx);
            return next;
        }
        catch
        {
            return false;
        }
    }

    private static IAudioEndpointVolume? GetEndpointVolumeInterface(EDataFlow flow)
    {
        var enumerator = CreateEnumerator();
        if (enumerator == null) return null;

        if (enumerator.GetDefaultAudioEndpoint(flow, ERole.eMultimedia, out var device) != 0 || device == null)
        {
            if (enumerator.GetDefaultAudioEndpoint(flow, ERole.eCommunications, out device) != 0 || device == null)
                return null;
        }

        Guid iidEpVol = typeof(IAudioEndpointVolume).GUID;
        if (device.Activate(ref iidEpVol, CLSCTX_ALL, IntPtr.Zero, out object epObj) != 0)
            return null;

        return epObj as IAudioEndpointVolume;
    }

    private static IMMDeviceEnumerator? CreateEnumerator()
    {
        Type? type = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
        if (type == null) return null;
        return Activator.CreateInstance(type) as IMMDeviceEnumerator;
    }

    private sealed class AppSessionGroup
    {
        public HashSet<uint> ProcessIds { get; } = new();
        public string Title { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public ImageSource? Icon { get; set; }
        public bool IsActivePlaying { get; set; }
        public double VolumePercent { get; set; }
        public bool IsMuted { get; set; }
    }

    #region CoreAudio & PolicyConfig COM Interop

    private const int CLSCTX_ALL = 23;
    private const int DEVICE_STATE_ACTIVE = 0x00000001;
    private const int STGM_READ = 0x00000000;

    private enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2
    }

    private enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort r1;
        public ushort r2;
        public ushort r3;
        public IntPtr pwszVal;
        public IntPtr p2;
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out IMMDeviceCollection? ppDevices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice? ppEndpoint);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice? ppDevice);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr pClient);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(out uint pcDevices);

        [PreserveSig]
        int Item(uint nDevice, out IMMDevice? ppDevice);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

        [PreserveSig]
        int OpenPropertyStore(int stgmAccess, out IPropertyStore? ppProperties);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);

        [PreserveSig]
        int GetState(out int pdwState);
    }

    [ComImport]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
    }

    [ComImport]
    [Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr ppFormat);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out IntPtr ppFormat);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, IntPtr key, IntPtr pv);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, IntPtr key, IntPtr pv);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bVisible);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr AudioSessionGuid, uint StreamFlags, out IntPtr SessionControl);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr AudioSessionGuid, uint StreamFlags, out ISimpleAudioVolume SessionVolume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator? SessionEnum);
        [PreserveSig] int RegisterSessionNotification(IntPtr SessionNotification);
        [PreserveSig] int UnregisterSessionNotification(IntPtr SessionNotification);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionID, IntPtr duckNotification);
        [PreserveSig] int UnregisterDuckNotification(IntPtr duckNotification);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int SessionCount);
        [PreserveSig] int GetSession(int SessionCount, out IAudioSessionControl2? Session);
    }

    [ComImport]
    [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int pRetVal);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string? pRetVal);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string? pRetVal);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] int GetGroupingParam(out Guid pRetVal);
        [PreserveSig] int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr NewNotifications);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr NewNotifications);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string? pRetVal);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string? pRetVal);
        [PreserveSig] int GetProcessId(out uint pRetVal);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float fLevel, ref Guid EventContext);
        [PreserveSig] int GetMasterVolume(out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid EventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    #endregion
}
