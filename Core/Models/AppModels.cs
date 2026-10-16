using System.Text.Json.Serialization;
using ChattyWidgets.Core.Win32;

namespace ChattyWidgets.Core.Models;

public class WidgetInstanceSettings
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("left")]
    public double Left { get; set; } = 100;

    [JsonPropertyName("top")]
    public double Top { get; set; } = 100;

    [JsonPropertyName("width")]
    public double Width { get; set; } = 300;

    [JsonPropertyName("height")]
    public double Height { get; set; } = 200;

    [JsonPropertyName("layer")]
    public WidgetLayer Layer { get; set; } = WidgetLayer.Desktop;

    [JsonPropertyName("isClickThrough")]
    public bool IsClickThrough { get; set; } = false;

    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; set; } = false;

    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 1.0;

    [JsonPropertyName("scale")]
    public double Scale { get; set; } = 1.0;

    [JsonPropertyName("autoHideAnchorDot")]
    public bool AutoHideAnchorDot { get; set; } = false;
}

public class AppSettings
{
    [JsonPropertyName("startWithWindows")]
    public bool StartWithWindows { get; set; } = false;

    [JsonPropertyName("snapToGrid")]
    public bool SnapToGrid { get; set; } = true;

    [JsonPropertyName("gridSize")]
    public int GridSize { get; set; } = 10;

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "dark";

    [JsonPropertyName("remoteCatalogUrl")]
    public string RemoteCatalogUrl { get; set; } = "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/catalog.json";

    [JsonPropertyName("widgets")]
    public Dictionary<string, WidgetInstanceSettings> Widgets { get; set; } = new();
}

public class StoreItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "General";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "web";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public string Size { get; set; } = "15 KB";

    [JsonPropertyName("authorGithub")]
    public string AuthorGithub { get; set; } = string.Empty;

    [JsonPropertyName("authorUrl")]
    public string AuthorUrl { get; set; } = string.Empty;

    [JsonPropertyName("isOfficial")]
    public bool IsOfficial { get; set; } = false;

    [JsonPropertyName("trustLevel")]
    public string TrustLevel { get; set; } = "community";

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsInstalled { get; set; } = false;
}
