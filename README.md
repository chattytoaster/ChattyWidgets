# ✦ ChattyWidgets 1.0.2 — Native Desktop Widget Engine for Windows (.NET 8)

> **Language / Язык:** **[🇬🇧 English](README.md)** • **[🇷🇺 Русский](README.ru.md)**

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011%20x64-blue.svg)](https://microsoft.com)
[![Framework](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Widgets](https://img.shields.io/badge/official%20widgets-12%20native-orange.svg)](https://github.com/chattytoaster/ChattyWidgetsLibrary)

**ChattyWidgets 1.0.2** is an ultra-lightweight, high-performance, and visually stunning desktop widget engine for Windows 10 & 11 built on .NET 8, WPF, and DirectX. It natively supports glassmorphic XAML widgets, web widgets (HTML5/CSS3/JS via Microsoft Edge WebView2), desktop layer management (`WorkerW`/`Progman` desktop pinning, normal window, and always-on-top), `.chw` packages cryptographically signed with RSA-4096, desktop layout presets (`.chwp`), and an autonomous AI agent development skill (`/chw`).

---

## 🚀 Key Advantages & Architecture

- **Ultra-Low Resource Footprint**:
  - Native XAML Widgets: **0% CPU usage** when idle, hardware-accelerated DirectX rendering, minimal RAM footprint (~15–25 MB per instance).
  - Web Widgets: Isolated in lightweight WebView2 processes with a secure two-way `window.chatty` bridge.
- **Deep Windows Desktop Integration (Rainmeter-style)**:
  - **Desktop Layer (`WorkerW` / `Progman`)**: Pins widgets behind all application windows directly onto the desktop wallpaper; never minimizes when pressing `Win + D` ("Show Desktop").
  - **Normal & TopMost Layers**: Switchable on the fly.
  - **Click-Through Mode (`WS_EX_TRANSPARENT`)**: Forwards mouse clicks through transparent regions directly to desktop icons and background apps.
  - **Smart Snap-to-Grid & Screen Edges**: Magnetic snapping to monitor edges and configurable grid intervals (5px to 40px).
- **Desktop Presets (`.chwp`)**:
  - Save and restore entire multi-monitor widget layouts in one click.
  - Quick workspace switching in the Hub (`Default`, `Work`, `Monitoring`, `Compact`).
  - Export and import `.chwp` files with automatic detection of missing widgets and a 1-click installation prompt.
- **Always-Resizable Mode**:
  - Global setting in Hub Settings («Always Resizable Widgets» / `AppSettings.AlwaysResizable`) allowing interactive resizing of any widget by dragging borders, regardless of manifest constraints.
  - Per-instance free resizing toggle in the context menu.
- **Universal Running Marquee Text**:
  - Smooth automatic horizontal ticker scrolling (ping-pong or continuous loop) whenever text (song titles, news headlines, crypto prices) exceeds container bounds.
  - Automatically pauses on mouse hover.
- **Automatic GitHub Update Checks**:
  - Silent background release checks with an unobtrusive banner in the Hub and manual checking in the About settings section.
- **Bilingual Interface & Adaptive Themes**:
  - Instant on-the-fly switching between **English 🇬🇧** and **Russian 🇷🇺** across the Hub, settings windows, and widgets without restarting.
  - Dark 🌙 and Light ☀️ themes with automatic contrast and icon adaptation.
- **Security & Signed `.chw` Packages**:
  - Secure zip containers with strict manifest validation (`manifest.json`).
  - All official widgets are signed with RSA-4096 cryptographic signatures (`✦ Official` badge).
  - Native Windows file and folder picker integration.

---

## 📦 Catalog of 12 Official Native Widgets

Available in 1 click from the built-in Store powered by the official [**ChattyWidgetsLibrary**](https://github.com/chattytoaster/ChattyWidgetsLibrary):

| Icon | Widget | ID | Features & Description |
| :---: | :--- | :--- | :--- |
| ⏱️ | **Timer & Focus** | `com.chatty.pomodoro-timer` | Regular countdown timer on 1st place with quick buttons (+1m, +5m, +10m), Pomodoro Focus & Break intervals, audio completion alerts. |
| 🕒 | **Glass Clock** | `com.chatty.clock-glass` | World timezones and major cities (UTC, London, New York, Tokyo, Moscow, etc.), 12/24-hour formats, seconds toggle, localized date. |
| 📊 | **System Gauges** | `com.chatty.system-gauges` | CPU, RAM, system SSD, and network throughput monitor. Linear bars, circular rings, and compact 2-in-a-row grid layout. |
| 🎮 | **GPU Monitor** | `com.chatty.gpu-monitor` | Hardware DirectX/DWM counters: 3D Core load %, dedicated VRAM (GB and %), temperature, circular rings and linear styles. |
| 🎵 | **Mini Music Player** | `com.chatty.music-player` | Windows SMTC integration (Spotify, Yandex.Music, YouTube, web browsers), realistic spinning vinyl disc, running text marquee without button clipping. |
| 🌤️ | **Weather Card** | `com.chatty.weather-card` | Real-time weather and 5-day forecast (Open-Meteo), accurate city search, clean short units (m/s, mmHg, km/h, hPa). |
| 💎 | **Crypto & Currency** | `com.chatty.crypto-tracker` | Live rates for any crypto and fiat pairs (BTC, ETH, SOL, Gram [GRAM 💎], USD, EUR, RUB, GBP) via Binance API and CoinGecko. |
| 📝 | **Sticky Notes & Tasks** | `com.chatty.sticky-notes` | Interactive task checklist with persistent state, task counters, smart empty state, 5 accent colors (Amber, Sky, Emerald, Violet, Slate). |
| 🚀 | **Quick Dock** | `com.chatty.quick-dock` | Floating application launcher, custom file and icon selection via Windows Explorer, horizontal and vertical orientation. |
| 📅 | **Glass Calendar** | `com.chatty.calendar-glass` | Interactive calendar with month flipping, first day of week selection (Monday / Sunday), and today highlight. |
| 🖼️ | **Media Photo Frame** | `com.chatty.media-frame` | Local photo albums and single photo mode, rounded photo corners, centered cropping, auto-hiding navigation arrows. |
| 📰 | **RSS News Reader** | `com.chatty.rss-reader` | Live news ticker: UN News in Russian (un_ru), Euronews, Tech & AI, Habr, Crypto, Science, and custom RSS feed URLs. |

---

## 🛠 Installation & Quick Start

### Method 1: Bilingual Installer (Recommended)
1. Download the latest **`ChattyWidgetsSetup.exe`** from [**Releases**](https://github.com/chattytoaster/ChattyWidgets/releases).
2. Run the installer. It features a modern glassmorphic UI with runtime **EN 🇬🇧 / RU 🇷🇺** language switching:
   - Choose destination folder (defaults to `%LocalAppData%\Programs\ChattyWidgets`).
   - Create Desktop and Start Menu shortcuts.
   - Configure Windows autostart.
   - Register system file associations for **`.chw`** (Widget Packages) and **`.chwp`** (Desktop Presets).
   - **Install the AI Agent Skill (`/chw`)** for Google Antigravity, Claude Code, and Cursor.

### Method 2: Build From Source
- **Operating System**: Windows 10 (version 1809+) or Windows 11 (x64)
- **SDK**: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
# 1. Clone repository
git clone https://github.com/chattytoaster/chattywidgets.git
cd chattywidgets

# 2. Build solution
dotnet build -c Release

# 3. Run application
dotnet run -c Release
```

### Run Built-in Engine Self-Tests
```powershell
dotnet run -- --test
```

---

## 🤖 AI Agent Skill (`/chw`)

ChattyWidgets features built-in pair programming support for agentic AI tools (Google Antigravity, Claude Code, Cursor, Windsurf, etc.):
- Installed automatically by `ChattyWidgetsSetup.exe` into `~/.agents/skills/chw/`.
- Triggered with **`/chw`** in chat to let AI agents autonomously:
  1. Scaffold widget packages (`manifest.json`, `Widget.xaml`).
  2. Implement ViewModels and register localization keys (EN/RU).
  3. Design adaptive glassmorphic XAML UI with dark/light theme support.
  4. Create widget configuration settings cards.
  5. Package, cryptographically sign, and locally test widgets on the desktop.
  6. Publish verified widgets to `ChattyWidgetsLibrary`.

---

## ⌨️ Global Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| **`Win + Alt + W`** | Toggle visibility of all active desktop widgets |
| **`Win + Alt + P`** | Peek mode — temporarily brings widgets to the foreground |
| **`Win + Alt + H`** | Open ChattyWidgets Management Hub |

---

## 📦 Widget Package Specification (`.chw`)

A `.chw` package is a standard zip container with the following structure:
```text
my-widget.chw
├── manifest.json       # Metadata, permissions, dimensions, and entry point
├── icon.png            # Widget icon (64x64 or 128x128 px)
├── Widget.xaml         # Native XAML layout (for type="native")
└── index.html          # Web widget frontend (for type="web")
```

Example `manifest.json`:
```json
{
  "id": "com.developer.my-widget",
  "name": "My Widget",
  "name_ru": "Мой Виджет",
  "version": "1.0.0",
  "author": "Developer",
  "description": "Clean, glassmorphic desktop widget for Windows",
  "type": "native",
  "entry": "Widget.xaml",
  "icon": "icon.png",
  "defaultWidth": 280,
  "defaultHeight": 180,
  "minWidth": 160,
  "minHeight": 100,
  "resizable": true,
  "defaultLayer": "desktop",
  "clickThrough": false,
  "permissions": ["system", "storage"]
}
```

---

## 🛠 Developer Workshop

1. Open the **🛠 Workshop** tab in the Hub.
2. Enter widget name, ID, author, select runtime (**Native** or **Web**), and click **«✨ Create Scaffold»**.
3. Edit files in your favorite IDE — changes reload instantly thanks to **Hot Reload**.
4. Click **«📦 Build .chw Package»** to package and sign your widget for distribution.

---

## 📄 License

This project is licensed under the **MIT License**. See the `LICENSE` file for details.
