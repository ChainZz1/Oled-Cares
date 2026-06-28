# 🖥️ OLED Cares

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-blue.svg)](https://www.microsoft.com/windows)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.0%2B-green.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-purple.svg)](#license)
[![Executable Size](https://img.shields.io/badge/size-23%20KB-orange.svg)](#technical-details)

A lightweight, zero-dependency Windows system tray utility designed to instantly protect your OLED panels, save power, and ensure privacy. Trigger a pitch-black overlay or put your monitors to sleep with a global hotkey.

---

## 💡 Why OLED Cares?

OLED monitors offer infinite contrast and outstanding image quality, but they suffer from two major challenges: **burn-in** from static elements and **window rearrangement** when screens physically power off. 

`OLED Cares` solves these issues with dual-mode protection:

| Feature / Aspect | Mode 1: OLED Black Screen (Recommended) | Mode 2: Physical Display Sleep |
| :--- | :--- | :--- |
| **How it works** | Renders a borderless, pitch-black window on all active displays. | Sends a Win32 command (`SC_MONITORPOWER`) to turn off monitors. |
| **OLED Protection** | **100% Effective.** OLED pixels turn off completely when displaying absolute black (#000000), drawing zero power and emitting zero light. | **100% Effective.** Screens physically enter standby mode. |
| **Window Preservation** | **Yes.** Windows believes monitors are still on, preventing open windows, desktop icons, and app positions from shifting. | **No.** Windows may detect screen disconnection, causing apps to move to a single screen upon waking. |
| **Wake Trigger** | Instant on mouse movement (with jitter threshold) or any keypress. | System wake-up via mouse movement or keypress. |
| **Power Consumption** | Extremely low (mimics physical off for OLED panel). | Lowest (entire monitor enters standby). |

---

## ✨ Features

*   **Dual Screen-Care Modes:** Switch between a pitch-black overlay or native Windows screen sleep.
*   **Multi-Monitor Support:** Seamlessly covers all active displays in OLED Black Screen mode.
*   **Global Hotkey Hook:** Instantly trigger display protection from anywhere in Windows (Default: `CTRL+ALT+Z`).
*   **Jitter-Resistant Wake:** Prevents accidental wake-ups from micro-vibrations of the mouse (threshold: >15 pixels).
*   **Automatic Startup:** Option to launch silently minimized in the system tray when Windows boots.
*   **Zero-Dependency & Tiny Footprint:** Compiled to a tiny **23 KB** executable. Consumes **0% CPU** and **< 10MB RAM** in the background.
*   **Modern Aesthetics:** Uses Windows 11 Acrylic blur / Mica backdrop (with charcoal dark-theme fallbacks) for the settings dashboard.

---

## 🚀 Quick Start & How to Use

1. **Run:** Download and launch [OledCares.exe](file:///e:/Projects/oled%20cares/OledCares.exe).
2. **Setup:** On the first launch, the **Settings Dashboard** will display:
    * Choose your protection mode.
    * Set your preferred global shortcut keys.
    * Check "Start automatically with Windows" to run on startup.
3. **Minimize to Tray:** Clicking close `×` or minimize `–` hides the application to the Windows System Tray (near the system clock).
4. **Trigger:** Press your registered hotkey (e.g., `CTRL+ALT+Z`) to turn off the screens.
5. **Wake:** Move the mouse or press any key to resume work instantly.

> [!TIP]
> Right-click the system tray icon to open settings, trigger screen off immediately, or exit the application.

---

## 🛠️ How to Compile (No Tools Required!)

The project is built entirely in raw C# with zero external dependencies. You do not need to install Visual Studio or any heavy SDKs to build it, as Windows includes a built-in C# compiler.

To recompile:
1. Open the project folder.
2. Double-click [compile.bat](file:///e:/Projects/oled%20cares/compile.bat).
3. The script will locate your native Windows .NET Framework compiler (`csc.exe`) and generate a fresh `OledCares.exe` instantly.

---

## ⚙️ Technical Details (Under the Hood)

For developers interested in the Windows API integrations:

*   **Global Hotkeys:** Registered using the Win32 `RegisterHotKey` API with `MOD_NOREPEAT` to prevent rapid-fire triggers.
*   **Physical Standby:** Executed by sending a `WM_SYSCOMMAND` with `SC_MONITORPOWER` (parameter `2` for power-off) to prevent locking on hung background graphics drivers.
*   **Idle Timeout Monitoring:** Tracks inactivity via Win32 `GetLastInputInfo` to automatically trigger physical screen power-off after your system display timeout has elapsed while the Black Screen overlay is active.
*   **Visual Styling:** Uses `DwmSetWindowAttribute` to query round window corners and transients (Acrylic/Mica background) under Windows 11.
*   **Persistence:** Saved to `%APPDATA%\OledCares\config.txt` for easy backup.

---

## 📄 License

This project is open-source and licensed under the MIT License.
