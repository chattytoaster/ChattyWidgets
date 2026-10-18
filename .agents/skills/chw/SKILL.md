---
name: chw
description: >-
  Create, scaffold, implement, test, and publish desktop widgets for ChattyWidgets.
  Trigger with `/chw` or when requested to develop a new widget, create glassmorphic XAML UI,
  implement widget ViewModels, configure manifest.json, wire up custom settings cards,
  package .chw files, and publish signed packages to ChattyWidgetsLibrary.
---

# ChattyWidgets Widget Development & Publishing Skill (`/chw`)

Use this skill to autonomously scaffold, develop, wire up, locally test, and officially publish desktop widgets for the ChattyWidgets platform.

---

## Workflow Overview

Follow this 6-step sequential pipeline:

```
[1. Define ID & Manifest] ➔ [2. Implement ViewModel & Loc] ➔ [3. Design Glass XAML UI] ➔ [4. Build Settings Card] ➔ [5. Pack, Build & Local Test] ➔ [6. User Approval & Publish]
```

---

## Step-by-Step Procedure

### Step 1: Define Widget ID and Manifest

1. Determine the unique widget ID:
   - Official / Bundled widget: `com.chatty.<name>` (e.g. `com.chatty.speedometer`).
   - Community widget: `com.github.<username>.<name>` or `<username>.<name>`.
2. Create `manifest.json` following [Manifest Specification](file:///C:/Users/ChattyNB/.agents/skills/chw/references/manifest-spec.md):
   - Provide English `name` and Russian `name_ru`.
   - Provide English `description` and Russian `description_ru`.
   - Specify `category`, default `width`, `height`, `minWidth`, `minHeight`.
   - Set `"isTransparent": true`, `"entryPoint": "Widget.xaml"`.
   - Declare default `customSettings` keys.
3. Save to:
   - `C:\Users\ChattyNB\Projects\ChattyWidgetsLibrary\widgets\<widget-id>\manifest.json`

---

### Step 2: Implement ViewModel and Localization

If the widget has native logic, timers, system sensors, or custom settings:

1. **Create the ViewModel** in `Core/Services/NativeWidgetContextProvider.cs`:
   - Inherit from `NativeViewModelBase`.
   - Read saved settings in constructor from `win.Settings.CustomSettings`.
   - Handle property changes and overrides for `OnThemeChanged()` and `OnOpacityChanged()`.
   - Register the factory method in `NativeWidgetContextProvider.CreateContext()`.
2. **Register Localized Strings** in `Core/Services/LocalizationService.cs`:
   - Add both Russian and English strings with `Add("Key", "RU", "EN")`.
3. Reference: [Settings & ViewModel Integration](file:///C:/Users/ChattyNB/.agents/skills/chw/references/settings-integration.md).

---

### Step 3: Design Widget UI (XAML or Web)

#### Option A: Native Glassmorphic XAML UI
Create `Widget.xaml` following [XAML Glass Patterns](file:///C:/Users/ChattyNB/.agents/skills/chw/references/xaml-glass-patterns.md):
1. Root `<Border>` with `CornerRadius="20"`, `Background="#D8121622"`, `BorderBrush="#25FFFFFF"`, `ClipToBounds="True"`.
2. **Dark & Light Theme Adaptability**: Ensure high contrast in both themes (no white text on white backgrounds).
3. **Running Marquee Text**: Wrap overflow text in a container with `ClipToBounds="True"` and add `controls:MarqueeBehavior.EnableMarquee="True"`.
4. **Button Templates**: Use flat rounded vector templates instead of default WPF gray buttons.
5. Save `Widget.xaml` in `ChattyWidgetsLibrary\widgets\<widget-id>\Widget.xaml`.

#### Option B: Web Widgets (`index.html`) & Eliminating Square Backgrounds and Shadows
Create `index.html` following [Web Widget Patterns](file:///c:/Users/ChattyNB/Projects/chattywidgets/.agents/skills/chw/references/web-widget-patterns.md):

**Golden Rules for Transparency & Eliminating Background / Shadow Artifacts:**
1. **Root Transparency**:
   - `html, body { background: transparent !important; background-color: transparent !important; margin: 0; padding: 0; overflow: hidden; }`.
   - Never declare background color, border, or box-shadow on `html` or `body`.
2. **Never Use `filter: drop-shadow` on Outer Containers**:
   - In Chromium / WebView2, applying CSS `filter: drop-shadow(...)` to an outer HTML `<div>` (even with `border-radius: 50%`) forces the Skia compositor to allocate an intermediate rectangular blur tile. Residual alpha leaks into the bounding box corners, creating a visible faint square shadow/halo artifact on the desktop.
   - For non-rectangular / circular containers: **always use `filter: none !important; box-shadow: none !important;`**. Vector shadows should be applied only to inner SVG child elements (e.g. clock hands, dial marks).
3. **Do Not Apply `.widget-card` to Non-Rectangular Widgets**:
   - The engine's theme engine targets `.widget-card` to dynamically adjust glass opacity. Non-rectangular widgets (clocks, meters, badges) must use their own specific class (e.g. `.clock-container`) and NOT `.widget-card`.
4. **Engine-Level Guarantees**:
   - `WidgetWindow` uses `ResizeMode="NoResize"` (eliminates `WS_THICKFRAME` and DWM rectangular sizing frames).
   - `DWMWA_NCRENDERING_POLICY = DWMNCRP_DISABLED` (suppresses all DWM non-client window shadows).
   - `DefaultBackgroundColor` and WPF composition target use `Color.FromArgb(0, 0, 0, 0)` (prevents RGB=255 GPU blending artifacts).
5. **Native Control Dot**:
   - Widget authors do **not** need to create HTML control dots. The engine automatically overlays `AnchorDotOverlayWindow` on hover, handling drag-and-drop and context menus natively.
6. Save `index.html` in `ChattyWidgetsLibrary\widgets\<widget-id>\index.html`.

---

### Step 4: Configure Settings Schema

For community and modular widgets, **do NOT hardcode switch cases in C#**. Configure the settings declaratively in `manifest.json`:

1. **Declare `settingsSchema` in `manifest.json`**:
   Specify controls for your widget settings:
   - `boolean` / `bool`: Toggle checkbox
   - `select` / `options`: Dropdown combobox with options
   - `slider` / `range`: Value slider with min, max, step
   - `text` / `string`: Text input
   
   ```json
   "settingsSchema": [
     {
       "key": "mySetting",
       "type": "boolean",
       "title": "Enable Feature",
       "title_ru": "Включить функцию",
       "default": true
     }
   ]
   ```
   `WidgetSettingsWindow.BuildGenericSettings()` will automatically generate beautiful glassmorphic settings cards for all declared fields and handle real-time saving to `win.Settings.CustomSettings`.

2. **Official Core Widgets Only (`com.chatty.*`)**:
   Only bundled official widgets with complex native logic (e.g. city search with HTTP suggestions) implement a dedicated `Build<Name>Settings()` in `UI/WidgetSettingsWindow.xaml.cs`.

---

### Step 5: Package, Compile & Local Test

1. **Package the widget**:
   Run the packaging script in `ChattyWidgetsLibrary`:
   ```powershell
   cd C:\Users\ChattyNB\Projects\ChattyWidgetsLibrary
   python scripts/build_catalog.py
   ```
2. **Install package locally for testing**:
   ```powershell
   Copy-Item "packages\<widget-id>.chw" "$env:LOCALAPPDATA\ChattyWidgets\widgets\" -Force
   ```
3. **Compile host solution**:
   ```powershell
   cd C:\Users\ChattyNB\Projects\chattywidgets
   dotnet build ChattyWidgets.csproj -c Debug
   ```
   *Verify: 0 errors, 0 warnings.*
4. **Launch for user testing**:
   ```powershell
   Start-Process -FilePath "C:\Users\ChattyNB\Projects\chattywidgets\bin\Debug\net8.0-windows10.0.19041.0\ChattyWidgets.exe" -WorkingDirectory "C:\Users\ChattyNB\Projects\chattywidgets\bin\Debug\net8.0-windows10.0.19041.0"
   ```

---

### Step 6: User Approval Checkpoint & Remote Publishing

> [!IMPORTANT]
> Stop and report to the user. Present the created widget, describe how to test it, and ask for verification.
> **DO NOT commit or push to remote Git repositories until the user explicitly approves!**

Once the user replies with approval to publish:

1. **Commit and push to `ChattyWidgetsLibrary` (`origin/main`)**:
   ```powershell
   cd C:\Users\ChattyNB\Projects\ChattyWidgetsLibrary
   git add .
   git commit -m "feat(widgets): add <widget-id>"
   git push origin main
   ```
2. **Commit and push engine changes to `ChattyWidgets` (`origin/master`) if any**:
   ```powershell
   cd C:\Users\ChattyNB\Projects\chattywidgets
   git add .
   git commit -m "feat(engine): support settings and localization for <widget-id>"
   git push origin master
   ```
3. Reference: [Packaging & Publishing Guide](file:///C:/Users/ChattyNB/.agents/skills/chw/references/packaging-and-publishing.md).

---

## Output Contract

When executing `/chw`, provide a response with the following sections:
1. `Summary`: Widget ID, category, purpose, and visual style.
2. `Implemented Features`: UI elements, ViewModel properties, settings controls, and localization keys.
3. `Local Test Status`: Build output, package generation result, and desktop launch status.
4. `User Action Required`: Testing instructions and request for user approval to publish.

---

## When to Use

- When the user runs `/chw` or `/chw <widget-description>`.
- When asked to create, develop, or add a new desktop widget to ChattyWidgets.
- When packaging or publishing a new `.chw` package to the catalog.

---

## Limitations

- Does not bypass the user verification checkpoint: publishing to remote Git requires user confirmation.
- Only official `com.chatty.*` IDs are cryptographically signed with the official RSA key; community widgets must use the community namespace format.
- All UI must use WPF XAML and adhere to the glassmorphic theme standards.
