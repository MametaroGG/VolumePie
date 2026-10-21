using System;
using System.Windows.Media;

namespace VolumePie.Models;

public enum PieItemCategory
{
    MasterVolume,
    MicVolume,
    Brightness,
    AppAudio,
    SystemSounds,
    Navigation
}

public class PieItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public PieItemCategory Category { get; set; }

    private double _value;
    public double Value
    {
        get => _value;
        set => _value = Math.Clamp(value, 0.0, 100.0);
    }

    public string? CustomDisplayValue { get; set; }
    public bool IsActivePlaying { get; set; }
    public bool IsMuted { get; set; }
    public bool CanMute { get; set; } = true;
    public ImageSource? AppIcon { get; set; }
    public System.Windows.Media.Color AccentColor { get; set; } = Colors.DeepSkyBlue;

    public Action<double>? OnValueChanged { get; set; }
    public Func<bool>? OnToggleMute { get; set; }
    public Action? OnInvoked { get; set; }

    public void SetLevel(double newValue)
    {
        double clamped = Math.Clamp(newValue, 0.0, 100.0);
        if (Math.Abs(_value - clamped) < 0.05)
            return;

        _value = clamped;
        OnValueChanged?.Invoke(_value);
    }

    public void ToggleMute()
    {
        if (!CanMute || OnToggleMute == null)
            return;

        IsMuted = OnToggleMute.Invoke();
    }
}
