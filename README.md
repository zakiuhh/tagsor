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

When you're sharing your screen on a call, recording a tutorial, or plugging two mice into the same PC, it's easy for viewers (or the other person) to lose track of the pointer or miss keyboard shortcuts[cite: 4].

**Tagsor** attaches a sleek, floating pill right beside your cursor[cite: 4]. It glides smoothly at ~120Hz, flips automatically so it never clips off the edge of your monitor, displays keystrokes with repeat counters, and gives you built-in spotlight, screen-drawing, and full customization tools on demand[cite: 4].

---

## Features

- **Silky-Smooth Cursor Tag** - Follows your mouse with subtle velocity tilt, double-click pop feedback, drag-hold badges (`LMB` / `RMB` / `MMB`), scroll-wheel micro-bounce, shake-to-locate surge, and configurable idle fade-out[cite: 4].
- **Live Screen Marker & Smart Shapes (`Ctrl + Alt + D`)** - Draw smooth Catmull-Rom curves over any window with a floating sidebar for **Pen / Highlighter**, **stroke widths**, **colors**, and **auto-fade vs. pinned strokes**[cite: 4]. Hold steady at the end of a stroke to auto-snap into **Circles, Ellipses, Squares, Rectangles, Triangles, Stars, Lines, Arrows,** or **Checkmarks**[cite: 4].
- **Spotlight Focus Dimmer (`Ctrl + Alt + S`)** - Softly dims the rest of your screen while keeping a clear circular spotlight locked onto your cursor[cite: 4].
- **Quick-Switch Status Chips (`Ctrl + Alt + 1 / 2 / 3`)** - Pop temporary presentation badges (**`👀 Look here`**, **`⏸ Hold on`**, **`✅ Done`**) right on your cursor pill[cite: 4].
- **Multi-Mouse Profiles & Custom Hex Colors** - Uses Windows Raw Input to tell different physical mice apart[cite: 4]. Give each mouse a custom name, pick from preset accent swatches or enter any custom **`#RGB` / `#RRGGBB` hex code** with live preview, and switch profiles manually or automatically whenever that mouse moves[cite: 4].
- **Tabbed Settings & Interactive Shortcut Recorder** - Customize idle fade delay, keystroke badge duration, Windows startup behavior, saved mouse profiles, and rebind any global hotkey directly from a clean, tabbed Settings window.
- **Live Keystroke Display & Repeat Counter** - Shows up to 3 recent keystrokes with crisp vector icons and stacks rapid consecutive presses (`×2`, `×3`) before smoothly reverting back to your name[cite: 4].
- **Built-In Privacy Protection** - Automatically ignores anything typed inside Windows password boxes, plus a customizable global **`Ctrl + Alt + H`** shortcut to hide keystrokes on demand[cite: 4].
- **Native Windows Look & Feel** - Automatically matches Windows Light and Dark mode, includes a custom rounded system tray menu and scrollbars, and launches quietly at startup[cite: 4].

---

## Quick Start

### Option 1: Download & Run (No Setup Needed)
1. Directly download from [**here**](https://github.com/zakiuhh/tagsor/releases/download/v1.3.0/Tagsor.exe) or go to the [**Releases**](../../releases) page, scroll to the Assets section and download **`Tagsor.exe`**[cite: 4].
2. Double-click **`Tagsor.exe`**[cite: 4].
3. A small **Tagsor** icon will appear in your Windows system tray (bottom-right near the clock), and your cursor tag will appear right away[cite: 4].

> **Notes:**
> - Because `Tagsor.exe` is an unsigned indie utility, Windows SmartScreen may show a prompt on the first launch[cite: 4]. Click **More info** → **Run anyway**[cite: 4].
> - When recording your screen, use **Full-Screen / Display Capture** (e.g., OBS Display Capture or Windows Snipping Tool) rather than single-window capture so the floating overlay, spotlight, and screen marker strokes are included in your video[cite: 4].

### Option 2: Build from Source
Make sure you have the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed on Windows 10 or 11[cite: 4, 5].

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

Click (left or right) the **Tagsor** icon in your system tray to open the quick menu, or trigger tools globally from anywhere (all shortcuts can be customized in **Settings → Shortcuts**)[cite: 4, 5]:

| Action | Default Shortcut / Trigger | What It Does |
| :--- | :--- | :--- |
| **Screen marker** | `Ctrl + Alt + D` or Tray Menu | Opens the floating annotation sidebar to draw smooth strokes or hold-to-snap shapes[cite: 4]. |
| **Spotlight focus** | `Ctrl + Alt + S` or Tray Menu | Dims the screen outside a soft circular spotlight around your cursor[cite: 4]. |
| **Status chips** | `Ctrl + Alt + 1 / 2 / 3` or Tray Menu | Displays **`👀 Look here`**, **`⏸ Hold on`**, or **`✅ Done`** for 5 seconds[cite: 4]. |
| **Dismiss active tool** | `Esc` | Immediately exits Draw Mode, turns off Spotlight, or clears an active status chip[cite: 4]. |
| **Hide keystrokes** | `Ctrl + Alt + H` or Tray Menu | Toggles Privacy Mode so typed keys are never shown on screen[cite: 4]. |
| **Settings** | Tray Menu → *Settings...* | Manage saved mouse profiles, timing sliders, startup toggle, and custom shortcuts. |
| **Rename mouse** | Tray Menu → *Rename mouse...* | Edit the display name and pick a preset or custom `#RRGGBB` accent color for the active mouse[cite: 4]. |
| **Pause tag** | Tray Menu → *Pause tag* | Temporarily hides the floating cursor pill without quitting the app[cite: 4]. |
| **Quit** | Tray Menu → *Quit* | Closes Tagsor completely[cite: 4]. |

---

## Where Profiles & Settings Are Saved

Your saved mouse profiles and preferences are stored locally on your machine in JSON format[cite: 4]:

```text
%AppData%\Tagsor\saved_mice.json
%AppData%\Tagsor\settings.json
```

Older versions used `%AppData%\DualCursorApp\saved_mice.json`[cite: 4]. Tagsor moves that file to the new location automatically the first time it starts[cite: 4].

*(Everything runs 100% locally on your PC-no internet connection, telemetry, or background accounts.)*[cite: 4]

---

## Tech Stack

- **C# / .NET 8 (WPF)** - Hardware-accelerated transparent overlays, Catmull-Rom spline rendering, custom tray popup, and animations[cite: 4].
- **RawInput.Sharp** - Low-level Windows `WM_INPUT` device identification for multi-mouse tracking and global key capture[cite: 4].
- **UI Automation & Win32 APIs** - Password field detection, global hotkeys, and Per-Monitor V2 High-DPI scaling[cite: 4].

---

## License

Distributed under the [MIT License](LICENSE) - feel free to use, modify, and share[cite: 4].
