<p align="center">
  <img src="assets/logo.png" alt="Tagsor Icon" width="110" height="110" />
</p>

<h1 align="center">Tagsor</h1>

<p align="center">
  <strong>A lightweight cursor tag, live keystroke badge, and screen annotation utility for Windows.</strong>
</p>

<p align="center">
  Built for screen recordings, live demos, pair programming, and shared setups—so viewers always know whose cursor is moving, what keys are being pressed, and where to look.
</p>

---

## Why Tagsor?

When you're sharing your screen on a call, recording a tutorial, or plugging two mice into the same PC, it's easy for viewers (or the other person) to lose track of the pointer or miss keyboard shortcuts.

**Tagsor** attaches a sleek, floating pill right beside your cursor. It glides smoothly at ~120Hz, flips automatically so it never clips off the edge of your monitor, displays keystrokes with repeat counters, and gives you built-in spotlight and screen-drawing tools on demand.

---

## Features

- **Silky-Smooth Cursor Tag** — Follows your mouse with subtle velocity tilt, double-click pop feedback, drag-hold badges (`LMB` / `RMB` / `MMB`), scroll-wheel micro-bounce, shake-to-locate surge, and automatic idle fade-out.
- **Live Screen Marker & Smart Shapes (`Ctrl + Alt + D`)** — Draw smooth Catmull-Rom curves over any window with a floating sidebar for **Pen / Highlighter**, **stroke widths**, **colors**, and **auto-fade vs. pinned strokes**. Hold steady at the end of a stroke to auto-snap into **Circles, Ellipses, Squares, Rectangles, Triangles, Stars, Lines, Arrows,** or **Checkmarks**.
- **Spotlight Focus Dimmer (`Ctrl + Alt + S`)** — Softly dims the rest of your screen while keeping a clear circular spotlight locked onto your cursor.
- **Quick-Switch Status Chips (`Ctrl + Alt + 1 / 2 / 3`)** — Pop temporary presentation badges (**`👀 Look here`**, **`⏸ Hold on`**, **`✅ Done`**) right on your cursor pill.
- **Multi-Mouse Profiles** — Uses Windows Raw Input to tell different physical mice apart. Plug in a second mouse, give it a custom name and accent color, and Tagsor switches tags automatically whenever that mouse moves.
- **Live Keystroke Display & Repeat Counter** — Shows up to 3 recent keystrokes with crisp vector icons and stacks rapid consecutive presses (`×2`, `×3`) before smoothly reverting back to your name.
- **Built-In Privacy Protection** — Automatically ignores anything typed inside Windows password boxes, plus a global **`Ctrl + Alt + H`** shortcut to hide keystrokes on demand.
- **Native Windows Look & Feel** — Automatically matches Windows Light and Dark mode, includes a custom rounded system tray menu, and launches quietly at startup.

---

## Quick Start

### Option 1: Download & Run (No Setup Needed)
1. Go to the [**Releases**](../../releases) page and download **`Tagsor.exe`**.
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

Click (left or right) the **Tagsor** icon in your system tray to open the quick menu, or trigger tools globally from anywhere:

| Action | Shortcut / Trigger | What It Does |
| :--- | :--- | :--- |
| **Screen marker** | `Ctrl + Alt + D` or Tray Menu | Opens the floating annotation sidebar to draw smooth strokes or hold-to-snap shapes. |
| **Spotlight focus** | `Ctrl + Alt + S` or Tray Menu | Dims the screen outside a soft circular spotlight around your cursor. |
| **Status chips** | `Ctrl + Alt + 1 / 2 / 3` or Tray Menu | Displays **`👀 Look here`**, **`⏸ Hold on`**, or **`✅ Done`** for 5 seconds. |
| **Dismiss active tool** | `Esc` | Immediately exits Draw Mode, turns off Spotlight, or clears an active status chip. |
| **Hide keystrokes** | `Ctrl + Alt + H` or Tray Menu | Toggles Privacy Mode so typed keys are never shown on screen. |
| **Rename mouse** | Tray Menu → *Rename mouse...* | Change the display name and accent glow color for the active mouse. |
| **Pause tag** | Tray Menu → *Pause tag* | Temporarily hides the floating cursor pill without quitting the app. |
| **Clear saved mice** | Tray Menu → *Clear saved mice* | Resets all saved mouse profiles back to default. |
| **Quit** | Tray Menu → *Quit* | Closes Tagsor completely. |

---

## Where Profiles Are Saved

Your saved mouse names and accent colors are stored locally on your machine in JSON format:

```text
%AppData%\Tagsor\saved_mice.json
```

Older versions used `%AppData%\DualCursorApp\saved_mice.json`. Tagsor moves that file to the new location automatically the first time it starts.

*(Everything runs 100% locally on your PC—no internet connection, telemetry, or background accounts.)*

---

## Tech Stack

- **C# / .NET 8 (WPF)** — Hardware-accelerated transparent overlays, Catmull-Rom spline rendering, custom tray popup, and animations.
- **RawInput.Sharp** — Low-level Windows `WM_INPUT` device identification for multi-mouse tracking and global key capture.
- **UI Automation & Win32 APIs** — Password field detection, global hotkeys, and Per-Monitor V2 High-DPI scaling.

---

## License

Distributed under the [MIT License](LICENSE) — feel free to use, modify, and share.
