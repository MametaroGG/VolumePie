<p align="center">
  <a href="README.md">日本語</a> | <b>English</b> | <a href="README.zh-CN.md">简体中文</a> | <a href="README.zh-TW.md">繁體中文</a> | <a href="README.ko.md">한국어</a>
</p>

# VolumePie - Radial Slider Menu (Audio Mixer, Microphone & Brightness)

A modern Windows radial pie menu that allows you to instantly adjust master volume, per-app volume mixer, microphone input volume, and screen brightness (Internal WMI + External DDC/CI + Software dimmer fallback) directly at your cursor position.

## 3 Slider Interaction Styles

You can switch between three unique slider interaction styles anytime in the Settings Window (VolumePie Customization Studio):

1. **1. Radial Gauge (Center-to-Outward Drag)**
   - Each pie slice functions as a radial gauge pointing outward from the center.
   - Drag radially outward or inward (or use mouse scroll wheel) to intuitively adjust between `0% - 100%`.
2. **2. Rotary Dial (Clockwise & Counter-Clockwise Rotation)**
   - Click and drag around the center in a **clockwise (+)** or **counter-clockwise (-)** circular motion for silky-smooth dial adjustment.
3. **3. Outer Arc Slider (Expandable Curve Slider)**
   - Hovering over a slice reveals an expanded arc-shaped curve slider along the outer perimeter.
   - Slide the thumb along the curve for precise control.

> **Tips & Tricks**
> - **Slider Drag**: Drag directly on a slice for smooth, real-time 1% precision adjustment.
> - **Mouse Scroll Wheel**: Hover over any slice and scroll to adjust quickly (default: 3% steps, customizable from 1% to 10% in settings). Hold `Shift` or `Ctrl` while scrolling for **1% ultra-fine precision mode**.
> - **Input & Output Device Switcher**: Click the top pills, scroll the wheel over them, or press `O` (output) / `I` (input) / `D` on your keyboard to instantly switch playback or recording devices. You can also switch them directly from the task tray menu.
> - **Close Menu**: Press `Esc`, right-click anywhere, or click the center hub to close immediately.

## Activation Triggers

- **Keyboard Shortcut (Default: `Alt + Q`)**
  - Fully customizable to any key combination in settings.
- **Mouse Button Trigger (Default: `Hold Middle Click`)**
  - Supports `Hold Middle Click`, `Hold Back (X1) Button`, `Hold Forward (X2) Button`, or `Right-Click + Left-Click`.
- **Smart Toggle & Hold Behavior**
  - A quick tap toggles the menu open to compare or adjust multiple apps. A long press opens the menu and automatically commits and closes upon releasing the button/keys.

## Download & Installation

1. Download the latest installer (`VolumePie-Setup-1.0.2.exe`) from the [Releases Page](https://github.com/MametaroGG/VolumePie/releases).
2. Run the downloaded setup file and follow the on-screen instructions.
3. Once installed, launch VolumePie from your Start Menu or desktop shortcut (it minimizes to the task tray automatically).

<details>
<summary>For Developers: Building from Source</summary>

Clone the repository and run the PowerShell build script to publish a self-contained binary and generate the Inno Setup installer:

```powershell
.\build-installer.ps1
```

- Application output: `publish\VolumePie.exe`
- Installer output: `dist\VolumePie-Setup-1.0.2.exe`
</details>
