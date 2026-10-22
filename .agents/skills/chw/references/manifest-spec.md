# ChattyWidgets Manifest Specification (`manifest.json`)

Every ChattyWidgets widget package (`.chw`) MUST contain a valid `manifest.json` in its root directory.

## JSON Schema & Fields

```json
{
  "$schema": "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/schema/widget.schema.json",
  "id": "com.chatty.my-widget",
  "name": "My Widget",
  "name_ru": "Мой Виджет",
  "version": "1.0.0",
  "author": "ChattyWidgets",
  "authorGithub": "chattytoaster",
  "description": "Clean, glassmorphic desktop widget with real-time updates.",
  "description_ru": "Стильный стеклянный виджет для рабочего стола с обновлениями в реальном времени.",
  "category": "utility",
  "entryPoint": "Widget.xaml",
  "defaultWidth": 320,
  "defaultHeight": 240,
  "minWidth": 240,
  "minHeight": 180,
  "maxWidth": 800,
  "maxHeight": 600,
  "isResizable": true,
  "isTransparent": true,
  "defaultOpacity": 1.0,
  "defaultLayer": "Normal",
  "customSettings": {
    "mySettingKey": "defaultValue"
  }
}
```

## Field Definitions

| Field | Type | Required | Description |
|---|---|---|---|
| `id` | `string` | **Yes** | Unique identifier in reverse-domain notation. For official starter widgets: `com.chatty.<name>`. For community widgets: `com.github.<username>.<name>` or `<username>.<name>`. |
| `name` | `string` | **Yes** | Display name in English (fallback for all locales). |
| `name_ru` | `string` | **Recommended** | Localized display name in Russian. |
| `version` | `string` | **Yes** | Semantic version string (e.g. `"1.0.0"`). |
| `author` | `string` | **Yes** | Author display name (e.g. `"ChattyWidgets"` for official). |
| `authorGithub` | `string` | Optional | GitHub username for avatar and trust attribution. |
| `description` | `string` | **Yes** | Brief English description of functionality and features. |
| `description_ru` | `string` | **Recommended** | Brief Russian description. |
| `category` | `string` | **Yes** | One of: `"system"`, `"utility"`, `"media"`, `"finance"`, `"weather"`, `"fun"`. |
| `type` | `string` | Optional | `"native"` (default, WPF XAML) or `"web"` (HTML5 / WebView2). |
| `entryPoint` / `entry` | `string` | **Yes** | Main entry file: `"Widget.xaml"` for native widgets, or `"index.html"` for web widgets. |
| `defaultWidth` / `width` | `number` | **Yes** | Default initial window width in virtual pixels (**320** recommended for standard 4:3 ratio). |
| `defaultHeight` / `height` | `number` | **Yes** | Default initial window height in virtual pixels (**240** recommended for standard 4:3 ratio). |
| `minWidth` | `number` | Optional | Minimum resizable width (**240** recommended for standard 4:3 ratio). |
| `minHeight` | `number` | Optional | Minimum resizable height (**180** recommended for standard 4:3 ratio). |
| `isResizable` / `resizable` | `boolean` | Optional | Whether user can drag edges to resize the widget (default `true`). |
| `isTransparent` | `boolean` | Optional | Must be `true` for glassmorphic borderless widgets (default `true`). |
| `defaultOpacity` | `number` | Optional | Default opacity from `0.15` to `1.0` (default `1.0`). |
| `defaultLayer` | `string` | Optional | Default desktop layer: `"Normal"`, `"Desktop"` (pinned to wallpaper), or `"TopMost"` (always on top). |
| `customSettings` | `object` | Optional | Default key-value pairs stored in widget settings dictionary. |

## Standard Proportions (4:3 Aspect Ratio)

To ensure that widgets placed on the desktop stand in a unified, beautiful row without jagged edges or uneven heights, ChattyWidgets adopts a standardized 4:3 aspect ratio:

- **Default Size**: `defaultWidth: 320`, `defaultHeight: 240` (100% scale).
- **Minimum Size**: `minWidth: 240`, `minHeight: 180` (75% scale).
- **Built-in Resize Presets**:
  - **25% (Мини)**: `80×60`
  - **50% (Компактный)**: `160×120`
  - **75% (Уменьшенный)**: `240×180`
  - **100% (Обычный)**: `320×240`
  - **125% (Увеличенный)**: `400×300`

All widgets should structure their internal layout (XAML or CSS) to comfortably render all essential content at the minimum `240×180` viewport and gracefully expand up to `480×360` and beyond.

## Official vs Community Rules

1. **Official Widgets** (`com.chatty.*`):
   - Only official widgets may use the `com.chatty.` namespace.
   - When signed with `keys/official_private.pem`, they receive the **✦ Официальный** (Official) trust badge and verified checkmark `ChattyWidgets ✔`.
2. **Community Widgets** (`com.github.<username>.*`):
   - Must use the creator's GitHub handle in the namespace.
   - Verified via Git provenance and catalog build.
