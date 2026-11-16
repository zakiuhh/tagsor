<p align="center">
  <img src="assets/logo.png" alt="Tagsor Icon" width="110" height="110" />
</p>

<h1 align="center">Tagsor</h1>

<p align="center">
  <strong>A lightweight cursor tag, live keystroke badge, and screen annotation utility for Windows.</strong>
</p>

<p align="center">
  Built for screen recordings, live demos, pair programming, and shared setups - so viewers always know whose cursor is moving, what keys are being pressed, and where to look.
</p>

<p align="center">
  <a href="https://github.com/zakiuhh/tagsor/releases/download/v1.3.0/Tagsor.exe">
      <img src="https://img.shields.io/badge/Download_for_Windows-Tagsor.exe_(v1.3.0)-0078D4?style=for-the-badge&logo=windows&logoColor=white"/>
  </a>
</p>

---

## Why Tagsor?

When you're sharing your screen on a call, recording a tutorial, or plugging two mice into the same PC, it's easy for viewers (or the other person) to lose track of the pointer or miss keyboard shortcuts.

**Tagsor** attaches a sleek, floating pill right beside your cursor. It glides smoothly at ~120Hz, flips automatically so it never clips off the edge of your monitor, displays keystrokes with repeat counters, and gives you built-in spotlight, screen-drawing, and live appearance customization tools on demand.

---

## Features

- **Silky-Smooth Cursor Tag** - Follows your mouse with subtle velocity tilt, double-click pop feedback, drag-hold badges (`LMB` / `RMB` / `MMB`), scroll-wheel micro-bounce, shake-to-locate surge, and configurable idle fade-out.
- **Live Pill Appearance Customization** - Adjust **pill scale** (`0.80x`–`1.40x`), **corner radius** (`4px`–`22px`), **opacity** (`40%`–`100%`), and **pointer offset position** (*Bottom-right*, *Top-right*, or *Centered below*) with real-time morphing previews.
- **Live Screen Marker & Smart Shapes (`Ctrl + Alt + D`)** - Draw smooth Catmull-Rom curves over any window with a floating sidebar for **Pen / Highlighter**, **stroke widths**, **colors**, and **auto-fade vs. pinned strokes**. Hold steady at the end of a stroke to auto-snap into **Circles, Ellipses, Squares, Rectangles, Triangles, Stars, Lines, Arrows,** or **Checkmarks**.
- **Spotlight Focus Dimmer (`Ctrl + Alt + S`)** - Softly dims the rest of your screen while keeping a clear circular spotlight locked onto your cursor.
- **Quick-Switch Status Chips (`Ctrl + Alt + 1 / 2 / 3`)** - Pop temporary presentation badges (**`👀 Look here`**, **`⏸ Hold on`**, **`✅ Done`**) right on your cursor pill.
- **Multi-Mouse Profiles & Custom Hex Colors** - Uses Windows Raw Input to tell different physical mice apart. Give each mouse a custom name, pick from preset accent swatches or enter any custom **`#RGB` / `#RRGGBB` hex code** with live preview, and switch profiles manually or automatically whenever that mouse moves.
- **Tabbed Settings & Interactive Shortcut Recorder** - Manage saved mouse profiles, live appearance sliders, timing delays, Windows startup behavior, one-click GitHub release updates, and rebind any global hotkey from a clean, native vector-icon Settings window.
- **Live Keystroke Display & Repeat Counter** - Shows up to 3 recent keystrokes with crisp vector icons and stacks rapid consecutive presses (`×2`, `×3`) before smoothly reverting back to your name.
- **Built-In Privacy Protection** - Automatically ignores anything typed inside Windows password boxes, plus a customizable global **`Ctrl + Alt + H`** shortcut to hide keystrokes on demand.
- **Native Windows Look & Feel** - Automatically matches Windows Light and Dark mode, includes a custom rounded system tray menu and scrollbars, and launches quietly at startup.

---

## Quick Start

### Option 1: Download & Run (No Setup Needed)
1. Directly download from [**here**](https://github.com/zakiuhh/tagsor/releases/download/v1.3.0/Tagsor.exe) or go to the [**Releases**](../../releases) page, scroll to the Assets section and download **`Tagsor.exe`**.
2. Double-click **`Tagsor.exe`**.
3. A small **Tagsor** icon will appear in your Windows system tray (bottom-right near the clock), and your cursor tag will appear right away.

> **Notes:**
> - Because `Tagsor.exe` is an unsigned indie utility, Windows SmartScreen may show a prompt on the first launch. Click **More info** → **Run anyway**.
> - When recording your screen, use **Full-Screen / Display Capture** (e.g., OBS Display Capture or Windows Snipping Tool) rather than single-window capture so the floating overlay, spotlight, and screen marker strokes are included in your video.

### Option 2: Build from Source
Make sure you have the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed on Windows 10 or 11.

```bash
git clone https://github.com/zakiuhh/tagsor.git
cd Tagsor
dotnet run
```

To package it into a single standalone `.exe`:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -o ./publish
```

---

## Controls & Shortcuts

Click (left or right) the **Tagsor** icon in your system tray to open the quick menu, or trigger tools globally from anywhere (all shortcuts can be customized in **Settings → Shortcuts**):

| Action | Default Shortcut / Trigger | What It Does |
| :--- | :--- | :--- |
| **Screen marker** | `Ctrl + Alt + D` or Tray Menu | Opens the floating annotation sidebar to draw smooth strokes or hold-to-snap shapes. |
| **Spotlight focus** | `Ctrl + Alt + S` or Tray Menu | Dims the screen outside a soft circular spotlight around your cursor. |
| **Status chips** | `Ctrl + Alt + 1 / 2 / 3` or Tray Menu | Displays **`👀 Look here`**, **`⏸ Hold on`**, or **`✅ Done`** for 5 seconds. |
| **Dismiss active tool** | `Esc` | Immediately exits Draw Mode, turns off Spotlight, or clears an active status chip. |
| **Hide keystrokes** | `Ctrl + Alt + H` or Tray Menu | Toggles Privacy Mode so typed keys are never shown on screen. |
| **Settings** | Tray Menu → *Settings...* | Manage saved mouse profiles, live pill appearance, timing sliders, updates, and custom shortcuts. |
| **Rename mouse** | Tray Menu → *Rename mouse...* | Edit the display name and pick a preset or custom `#RRGGBB` accent color for the active mouse. |
| **Pause tag** | Tray Menu → *Pause tag* | Temporarily hides the floating cursor pill without quitting the app. |
| **Quit** | Tray Menu → *Quit* | Closes Tagsor completely. |

---

## Where Profiles & Settings Are Saved

Your saved mouse profiles and preferences are stored locally on your machine in JSON format:

```text
%AppData%\Tagsor\saved_mice.json
%AppData%\Tagsor\settings.json
```

Older versions used `%AppData%\DualCursorApp\saved_mice.json`. Tagsor moves that file to the new location automatically the first time it starts.

*(Tagsor runs locally on your PC with zero telemetry or accounts—network requests are only made to GitHub Releases when checking for updates.)*

---

## Tech Stack

- **C# / .NET 8 (WPF)** - Hardware-accelerated transparent overlays, Catmull-Rom spline rendering, custom tray popup, and smooth animations.
- **RawInput.Sharp** - Low-level Windows `WM_INPUT` device identification for multi-mouse tracking and global key capture.
- **UI Automation & Win32 APIs** - Password field detection, global hotkeys, and Per-Monitor V2 High-DPI scaling.

---

## License

Distributed under the [MIT License](LICENSE) - feel free to use, modify, and share.
