using System.Text.Json.Serialization;

namespace ChattyWidgets.Core.Models;

public class WidgetManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "Unknown";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "web"; // "web" or "native"

    [JsonPropertyName("entry")]
    public string Entry { get; set; } = "index.html"; // "index.html" or "Widget.xaml"

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "icon.png";

    [JsonPropertyName("defaultWidth")]
    public double DefaultWidth { get; set; } = 300;

    [JsonPropertyName("defaultHeight")]
    public double DefaultHeight { get; set; } = 200;

    [JsonPropertyName("minWidth")]
    public double MinWidth { get; set; } = 150;

    [JsonPropertyName("minHeight")]
    public double MinHeight { get; set; } = 100;

    [JsonPropertyName("resizable")]
    public bool Resizable { get; set; } = true;

    [JsonPropertyName("defaultLayer")]
    public string DefaultLayer { get; set; } = "desktop"; // "desktop" | "normal" | "top"

    [JsonPropertyName("clickThrough")]
    public bool ClickThrough { get; set; } = false;

    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = new();
}
