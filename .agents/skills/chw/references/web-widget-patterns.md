# Web Widgets & Glassmorphic Transparency Patterns

Web widgets in ChattyWidgets run inside an isolated Microsoft Edge WebView2 host and can render modern HTML5, CSS3, Canvas, and SVG graphics with desktop-grade glassmorphism and full window transparency.

---

## 1. Window Transparency & Background Elimination Rule

When developing web widgets, **especially non-rectangular or circular widgets** (such as analog clocks, compasses, round gauges, floating badges):

### The "Square Background" Problem
The host desktop window is a rectangle (`width` x `height`). If any component paints a default background or border across the root, a faint square box will appear around circular or freeform widgets on the desktop.

### How to Eliminate the Square Background

1. **HTML & Body Transparency (Mandatory)**:
   In `index.html`, `<html>` and `<body>` must always be completely transparent with no margin or padding:
   ```css
   html, body {
     width: 100%;
     height: 100%;
     margin: 0;
     padding: 0;
     background: transparent !important;
     overflow: hidden;
     user-select: none;
     -webkit-user-select: none;
   }
   ```
   > [!WARNING]
   > Never declare `background-color`, `border`, or `box-shadow` on `html` or `body`.

2. **Scoped Shape Containers**:
   Apply all background colors, glass gradients, borders, and shadows exclusively to the inner widget container:
   - **For Circular Widgets (Clocks, Meters)**:
     ```css
     .clock-container {
       position: relative;
       width: 94vmin;
       height: 94vmin;
       border-radius: 50%;
       background: transparent !important;
       /* NEVER use filter: drop-shadow on the outer HTML div container!
          In Chromium Skia, drop-shadow on transparent windows allocates a rectangular
          blur layer that leaves a faint square shadow/halo artifact in the bounding box corners. */
       filter: none !important;
       box-shadow: none !important;
     }
     ```
   - **For Rounded Cards**:
     ```css
     .widget-card {
       width: calc(100% - 24px);
       height: calc(100% - 24px);
       margin: 12px;
       border-radius: 20px;
       background: rgba(18, 22, 34, var(--chatty-bg-opacity, 0.85));
       border: 1px solid rgba(255, 255, 255, 0.12);
       box-shadow: 0 12px 32px rgba(0, 0, 0, 0.45);
       backdrop-filter: blur(24px);
     }
     ```

3. **Engine-Level Transparency Guarantee**:
   The ChattyWidgets engine (`WidgetWindow.xaml.cs`) guarantees complete transparency for all web widgets (`"type": "web"`):
   - **Zeroed Alpha Color**: `WebView2.DefaultBackgroundColor` and WPF's `HwndSource.CompositionTarget.BackgroundColor` are set to `Color.FromArgb(0, 0, 0, 0)`. `Color.Transparent` (which is `ARGB(0, 255, 255, 255)`) is strictly avoided to prevent GPU DirectComposition white blending artifacts.
   - **No DWM Resize Frame**: `WidgetWindow.xaml` uses `ResizeMode="NoResize"`, eliminating the Win32 `WS_THICKFRAME` sizing border.
   - **Suppressed DWM Shadows**: `DwmSetWindowAttribute(_hwnd, DWMWA_NCRENDERING_POLICY, DWMNCRP_DISABLED)` completely removes rectangular desktop window shadows.
   - **Template Border Clearing**: The native WPF card border theme is skipped for web widgets, and any internal template borders have `Background`, `BorderBrush`, and `BorderThickness` zeroed out.

---

## 2. Native Engine Control Dot

Web widgets do **not** need to manually embed or style control dots:
- The engine automatically hosts an `AnchorDotOverlayWindow` pinned to the top-right corner of the widget.
- When the user hovers over the widget window, the blue glowing dot fades in automatically.
- Left-click on the dot allows moving the window; right-click opens the native ChattyWidgets context menu.

---

## 3. Window Dragging & Interactivity

The ChattyWidgets host automatically injects `window.chatty` into the WebView2 script environment:

- **Dragging Anywhere**: Clicking and dragging non-interactive areas of `.widget-card`, `.clock-container`, or elements with `data-chatty-drag` moves the widget smoothly.
- **Explicit Drag**:
  ```javascript
  myElement.addEventListener('mousedown', (e) => {
    if (e.button === 0) window.chatty.dragMove();
  });
  ```
- **Context Menu**:
  ```javascript
  myElement.addEventListener('contextmenu', (e) => {
    e.preventDefault();
    window.chatty.showContextMenu();
  });
  ```

---

## 4. Theme & Opacity CSS Variables

The engine dynamically injects CSS variables into `document.documentElement`:
- `--chatty-bg-opacity`: Current user background opacity (`0.15` to `1.0`).
- `--chatty-accent`: Current Windows Accent Color hex code (e.g. `#0078D7`).
- `--chatty-accent-glow`: Accent color with 40% alpha for glow shadows.
- `.light-theme` / `.dark-theme`: Toggled automatically on `<html>` when the desktop theme changes.
