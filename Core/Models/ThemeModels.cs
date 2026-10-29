using System.Text.Json.Serialization;

namespace ChattyWidgets.Core.Models;

public class ThemeDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("name_ru")]
    public string? NameRu { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; } = "User";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("baseTheme")]
    public string BaseTheme { get; set; } = "dark"; // "dark" or "light"

    [JsonPropertyName("colors")]
    public ThemeColors Colors { get; set; } = new();

    [JsonIgnore]
    public bool IsBuiltIn => Id == "dark" || Id == "light";

    [JsonIgnore]
    public string DisplayName =>
        ChattyWidgets.Core.Services.LocalizationService.Instance.IsRussian && !string.IsNullOrWhiteSpace(NameRu)
            ? NameRu
            : (!string.IsNullOrWhiteSpace(Name) ? Name : Id);

    public ThemeDefinition Clone() => new()
    {
        Id = Id,
        Name = Name,
        NameRu = NameRu,
        Author = Author,
        Version = Version,
        BaseTheme = BaseTheme,
        Colors = Colors.Clone()
    };
}

public class ThemeColors
{
    [JsonPropertyName("background")]
    public string Background { get; set; } = "#121622";

    [JsonPropertyName("cardBackground")]
    public string CardBackground { get; set; } = "#161928";

    [JsonPropertyName("border")]
    public string Border { get; set; } = "#28FFFFFF";

    [JsonPropertyName("textPrimary")]
    public string TextPrimary { get; set; } = "#F8FAFC";

    [JsonPropertyName("textSecondary")]
    public string TextSecondary { get; set; } = "#94A3B8";

    [JsonPropertyName("accent")]
    public string Accent { get; set; } = "#38BDF8";

    [JsonPropertyName("hover")]
    public string Hover { get; set; } = "#22273D";

    [JsonPropertyName("divider")]
    public string Divider { get; set; } = "#202434";

    public ThemeColors Clone() => new()
    {
        Background = Background,
        CardBackground = CardBackground,
        Border = Border,
        TextPrimary = TextPrimary,
        TextSecondary = TextSecondary,
        Accent = Accent,
        Hover = Hover,
        Divider = Divider
    };
}
