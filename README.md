<p align="center">
  <img src="assets/logo.png" alt="Tagsor Icon" width="110" height="110" />
</p>

<h1 align="center">Tagsor</h1>

<p align="center">
  <strong>A lightweight cursor tag and live keystroke badge for Windows.</strong>
</p>

<p align="center">
  Built for screen recordings, live demos, pair programming, and shared setups—so viewers always know whose cursor is moving and what keys are being pressed.
</p>

---

## Why Tagsor?

When you're sharing your screen on a call, recording a tutorial, or plugging two mice into the same PC, it's easy for viewers (or the other person) to lose track of the pointer or miss keyboard shortcuts.

**Tagsor** attaches a sleek, floating pill right beside your cursor. It glides smoothly at ~120Hz, flips automatically so it never clips off the edge of your monitor, and briefly turns into a clean keystroke badge whenever you type.

---

## Features

- **Silky-Smooth Cursor Tag** — Follows your mouse with subtle velocity tilt, click feedback, and scroll-wheel micro-bounce, fading out automatically when your cursor goes idle.
- **Multi-Mouse Profiles** — Uses Windows Raw Input to tell different physical mice apart. Plug in a second mouse, give it a custom name and accent color, and Tagsor switches tags automatically whenever that mouse moves.
- **Live Keystroke Display** — Shows up to 3 recent keystrokes with crisp vector icons (Enter, Shift, Ctrl, Alt, Win, Arrows, Backspace, Tab) before smoothly reverting back to your name after 1 second.
- **Built-In Privacy Protection** — Automatically ignores anything typed inside Windows password boxes, plus a global **`Ctrl + Alt + H`** shortcut to hide keystrokes on demand.
- **Native Windows Look & Feel** — Automatically matches Windows Light and Dark mode, includes a custom rounded system tray menu, and launches quietly at startup.

---

## Quick Start

### Option 1: Download & Run (No Setup Needed)
1. Go to the [**Releases**](../../releases) page and download **`Tagsor.exe`**.
2. Double-click **`Tagsor.exe`**.
3. A small **Tagsor** icon will appear in your Windows system tray (bottom-right near the clock), and your cursor tag will appear right away.

> **Note:** Because `Tagsor.exe` is an unsigned indie utility, Windows SmartScreen may show a prompt on the first launch. Click **More info** → **Run anyway**.

### Option 2: Build from Source
Make sure you have the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed on Windows 10 or 11.

```bash
git clone https://github.com/zakiuhh/tagsor.git
cd Tagsor
dotnet run
```

To package it into a single standalone `.exe`:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish
```

---

## Controls & Shortcuts

Click (left or right) the **Tagsor** icon in your system tray to open the quick menu:

| Action | How to Trigger | What It Does |
| :--- | :--- | :--- |
| **Rename mouse** | Tray Menu → *Rename mouse...* | Change the display name and accent glow color for the active mouse. |
| **Hide keystrokes** | `Ctrl + Alt + H` or Tray Menu | Toggles Privacy Mode so typed keys are never shown on screen. |
| **Pause tag** | Tray Menu → *Pause tag* | Temporarily hides the floating cursor pill without quitting the app. |
| **Clear saved mice** | Tray Menu → *Clear saved mice* | Resets all saved mouse profiles back to default. |
| **Quit** | Tray Menu → *Quit* | Closes Tagsor completely. |

---

## Where Profiles Are Saved

Your saved mouse names and accent colors are stored locally on your machine in JSON format:

```text
%AppData%\DualCursorApp\saved_mice.json
```

*(Everything runs 100% locally on your PC—no internet connection, telemetry, or background accounts.)*

---

## Tech Stack

- **C# / .NET 8 (WPF)** — Hardware-accelerated transparent overlay, custom tray popup, and animations.
- **RawInput.Sharp** — Low-level Windows `WM_INPUT` device identification for multi-mouse tracking and global key capture.
- **UI Automation & Win32 APIs** — Password field detection, global hotkeys, and Per-Monitor V2 High-DPI scaling.

---

## License

MIT License — feel free to use, modify, and share.
