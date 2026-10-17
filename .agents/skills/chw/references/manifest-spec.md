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
  "width": 300,
  "height": 200,
  "minWidth": 200,
  "minHeight": 120,
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
| `entryPoint` | `string` | **Yes** | Main XAML layout file. Must be `"Widget.xaml"`. |
| `width` | `number` | **Yes** | Default initial window width in virtual pixels (e.g. `280`, `320`). |
| `height` | `number` | **Yes** | Default initial window height in virtual pixels (e.g. `140`, `220`). |
| `minWidth` | `number` | Optional | Minimum resizable width (default `160`). |
| `minHeight` | `number` | Optional | Minimum resizable height (default `100`). |
| `isResizable` | `boolean` | Optional | Whether user can drag edges to resize the widget (default `true`). |
| `isTransparent` | `boolean` | Optional | Must be `true` for glassmorphic borderless widgets (default `true`). |
| `defaultOpacity` | `number` | Optional | Default opacity from `0.15` to `1.0` (default `1.0`). |
| `defaultLayer` | `string` | Optional | Default desktop layer: `"Normal"`, `"Desktop"` (pinned to wallpaper), or `"TopMost"` (always on top). |
| `customSettings` | `object` | Optional | Default key-value pairs stored in widget settings dictionary. |

## Official vs Community Rules

1. **Official Widgets** (`com.chatty.*`):
   - Only official widgets may use the `com.chatty.` namespace.
   - When signed with `keys/official_private.pem`, they receive the **✦ Официальный** (Official) trust badge and verified checkmark `ChattyWidgets ✔`.
2. **Community Widgets** (`com.github.<username>.*`):
   - Must use the creator's GitHub handle in the namespace.
   - Verified via Git provenance and catalog build.
