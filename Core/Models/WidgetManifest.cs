using System.Text.Json.Serialization;

namespace ChattyWidgets.Core.Models;

public class WidgetManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("name_ru")]
    public string? NameRu { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "Unknown";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("description_ru")]
    public string? DescriptionRu { get; set; }

    [JsonIgnore]
    public string DisplayName =>
        ChattyWidgets.Core.Services.LocalizationService.Instance.IsRussian && !string.IsNullOrWhiteSpace(NameRu)
            ? NameRu
            : (!string.IsNullOrWhiteSpace(Name) ? Name : Id);

    [JsonIgnore]
    public string DisplayDescription =>
        ChattyWidgets.Core.Services.LocalizationService.Instance.IsRussian && !string.IsNullOrWhiteSpace(DescriptionRu)
            ? DescriptionRu
            : Description;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "web"; // "web" or "native"

    private string _entry = "index.html";

    [JsonPropertyName("entry")]
    public string Entry
    {
        get => _entry;
        set => _entry = value;
    }

    [JsonPropertyName("entryPoint")]
    public string? EntryPoint
    {
        get => _entry;
        set { if (!string.IsNullOrEmpty(value)) _entry = value; }
    }

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

    [JsonPropertyName("menuItems")]
    public List<CustomMenuItem> MenuItems { get; set; } = new();

    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("backgroundColor")]
    public string? BackgroundColor { get; set; }

    [JsonPropertyName("injectedScript")]
    public string? InjectedScript { get; set; }

    [JsonPropertyName("customSettings")]
    public Dictionary<string, string> CustomSettings { get; set; } = new();

    [JsonPropertyName("settingsSchema")]
    public List<WidgetSettingSchemaItem> SettingsSchema { get; set; } = new();
}

public class CustomMenuItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

public class WidgetSettingSchemaItem
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "text"; // "text", "boolean", "select", "slider"

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("title_ru")]
    public string? TitleRu { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("description_ru")]
    public string? DescriptionRu { get; set; }

    [JsonPropertyName("default")]
    public string? DefaultValue { get; set; }

    [JsonPropertyName("min")]
    public double? Min { get; set; }

    [JsonPropertyName("max")]
    public double? Max { get; set; }

    [JsonPropertyName("step")]
    public double? Step { get; set; }

    [JsonPropertyName("options")]
    public List<WidgetSettingOption> Options { get; set; } = new();

    [JsonIgnore]
    public string DisplayTitle =>
        ChattyWidgets.Core.Services.LocalizationService.Instance.IsRussian && !string.IsNullOrWhiteSpace(TitleRu)
            ? TitleRu
            : (!string.IsNullOrWhiteSpace(Title) ? Title : Key);

    [JsonIgnore]
    public string? DisplayDescription =>
        ChattyWidgets.Core.Services.LocalizationService.Instance.IsRussian && !string.IsNullOrWhiteSpace(DescriptionRu)
            ? DescriptionRu
            : Description;
}

public class WidgetSettingOption
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("label_ru")]
    public string? LabelRu { get; set; }

    [JsonIgnore]
    public string DisplayLabel =>
        ChattyWidgets.Core.Services.LocalizationService.Instance.IsRussian && !string.IsNullOrWhiteSpace(LabelRu)
            ? LabelRu
            : (!string.IsNullOrWhiteSpace(Label) ? Label : Value);
}

