using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using VolumePie.Models;

namespace VolumePie.Services;

public class SettingsService
{
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "VolumePie";

    private readonly string _settingsFilePath;

    public AppSettings Current { get; private set; } = new();

    public event Action<AppSettings>? SettingsChanged;

    public SettingsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, "VolumePie");
        Directory.CreateDirectory(dir);
        _settingsFilePath = Path.Combine(dir, "settings.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    Current = loaded;
                }
            }
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public void Save(AppSettings? newSettings = null)
    {
        if (newSettings != null)
        {
            Current = newSettings;
        }

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Current, options);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Ignore write errors
        }

        SettingsChanged?.Invoke(Current);
    }

    public bool IsRunAtStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false);
            return key?.GetValue(AppRegistryName) != null;
        }
        catch
        {
            return false;
        }
    }

    public void SetRunAtStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true);
            if (key == null) return;

            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppRegistryName, $"\"{exePath}\" --minimized");
                }
            }
            else
            {
                key.DeleteValue(AppRegistryName, false);
            }
        }
        catch
        {
            // Ignore registry errors
        }
    }
}
