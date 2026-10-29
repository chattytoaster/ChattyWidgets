using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using ChattyWidgets.Core.Models;

namespace ChattyWidgets.Core.Services;

public class ThemeService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static ThemeService Instance { get; } = new();

    private readonly Dictionary<string, ThemeDefinition> _customThemes = new(StringComparer.OrdinalIgnoreCase);

    public event Action? ThemeListChanged;
    public event Action<ThemeDefinition>? ActiveThemeChanged;

    public void NotifyActiveThemeChanged(ThemeDefinition theme)
    {
        ActiveThemeChanged?.Invoke(theme);
    }

    public static readonly ThemeDefinition DarkTheme = new()
    {
        Id = "dark",
        Name = "Dark",
        NameRu = "Тёмная тема",
        Author = "ChattyWidgets",
        Version = "1.0.0",
        BaseTheme = "dark",
        Colors = new ThemeColors
        {
            Background = "#121622",
            CardBackground = "#161928",
            Border = "#28FFFFFF",
            TextPrimary = "#F8FAFC",
            TextSecondary = "#94A3B8",
            Accent = "#38BDF8",
            Hover = "#22273D",
            Divider = "#202434"
        }
    };

    public static readonly ThemeDefinition LightTheme = new()
    {
        Id = "light",
        Name = "Light",
        NameRu = "Светлая тема",
        Author = "ChattyWidgets",
        Version = "1.0.0",
        BaseTheme = "light",
        Colors = new ThemeColors
        {
            Background = "#F8FAFC",
            CardBackground = "#FFFFFF",
            Border = "#2D000000",
            TextPrimary = "#0F172A",
            TextSecondary = "#64748B",
            Accent = "#0284C7",
            Hover = "#E2E8F0",
            Divider = "#CBD5E1"
        }
    };

    public string ThemesDirectory => Path.Combine(ChwPackageService.AppDataDir, "themes");

    private ThemeService()
    {
    }

    public void Initialize()
    {
        try
        {
            Directory.CreateDirectory(ThemesDirectory);
            _customThemes.Clear();

            foreach (string file in Directory.GetFiles(ThemesDirectory, "*.chwtheme"))
            {
                LoadThemeFromFile(file);
            }

            foreach (string file in Directory.GetFiles(ThemesDirectory, "*.json"))
            {
                // In case users placed .json theme files manually
                string chwthemeEquivalent = Path.ChangeExtension(file, ".chwtheme");
                if (!File.Exists(chwthemeEquivalent))
                {
                    LoadThemeFromFile(file);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ThemeService] Initialize error: {ex.Message}");
        }
    }

    private void LoadThemeFromFile(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            var theme = JsonSerializer.Deserialize<ThemeDefinition>(json, JsonOpts);
            if (theme != null && !string.IsNullOrWhiteSpace(theme.Id) && !theme.IsBuiltIn)
            {
                _customThemes[theme.Id] = theme;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ThemeService] Failed to load theme '{filePath}': {ex.Message}");
        }
    }

    public IReadOnlyList<ThemeDefinition> GetAllThemes()
    {
        var list = new List<ThemeDefinition>
        {
            DarkTheme,
            LightTheme
        };
        list.AddRange(_customThemes.Values);
        return list;
    }

    public ThemeDefinition GetTheme(string? themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId) || themeId.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            themeId = WidgetManager.Instance?.Settings?.Theme ?? "dark";
        }

        if (themeId.Equals("light", StringComparison.OrdinalIgnoreCase))
            return LightTheme;

        if (themeId.Equals("dark", StringComparison.OrdinalIgnoreCase))
            return DarkTheme;

        if (_customThemes.TryGetValue(themeId, out var customTheme))
            return customTheme;

        return DarkTheme;
    }

    public void SaveCustomTheme(ThemeDefinition theme)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));

        if (string.IsNullOrWhiteSpace(theme.Id) || theme.IsBuiltIn)
        {
            theme.Id = "theme_" + Guid.NewGuid().ToString("N")[..8];
        }

        theme.Id = SanitizeFileName(theme.Id.Trim().ToLowerInvariant());

        Directory.CreateDirectory(ThemesDirectory);
        string targetFile = Path.Combine(ThemesDirectory, $"{theme.Id}.chwtheme");
        string json = JsonSerializer.Serialize(theme, JsonOpts);
        File.WriteAllText(targetFile, json);

        _customThemes[theme.Id] = theme;
        ThemeListChanged?.Invoke();
    }

    public bool DeleteCustomTheme(string themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId) || themeId.Equals("dark", StringComparison.OrdinalIgnoreCase) || themeId.Equals("light", StringComparison.OrdinalIgnoreCase))
            return false;

        string targetFile = Path.Combine(ThemesDirectory, $"{themeId}.chwtheme");
        if (File.Exists(targetFile))
        {
            try { File.Delete(targetFile); } catch { }
        }

        string jsonFile = Path.Combine(ThemesDirectory, $"{themeId}.json");
        if (File.Exists(jsonFile))
        {
            try { File.Delete(jsonFile); } catch { }
        }

        bool removed = _customThemes.Remove(themeId);
        if (removed)
        {
            // If active Hub theme was deleted, fallback to dark
            if (WidgetManager.Instance?.Settings?.Theme.Equals(themeId, StringComparison.OrdinalIgnoreCase) == true)
            {
                WidgetManager.Instance.ApplyGlobalTheme("dark");
            }
            ThemeListChanged?.Invoke();
        }
        return removed;
    }

    public ThemeDefinition ImportThemeFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Theme file not found", filePath);

        string json = File.ReadAllText(filePath);
        var theme = JsonSerializer.Deserialize<ThemeDefinition>(json, JsonOpts)
            ?? throw new InvalidOperationException("Invalid theme file format");

        if (string.IsNullOrWhiteSpace(theme.Name))
        {
            theme.Name = Path.GetFileNameWithoutExtension(filePath);
        }

        if (string.IsNullOrWhiteSpace(theme.Id) || theme.IsBuiltIn)
        {
            theme.Id = "imported_" + SanitizeFileName(theme.Name) + "_" + Guid.NewGuid().ToString("N")[..4];
        }

        SaveCustomTheme(theme);
        return theme;
    }

    public void ExportThemeToFile(string themeId, string destinationPath)
    {
        var theme = GetTheme(themeId);
        string json = JsonSerializer.Serialize(theme, JsonOpts);
        File.WriteAllText(destinationPath, json);
    }

    public static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        hex = hex.Trim();
        if (!hex.StartsWith("#")) hex = "#" + hex;
        try
        {
            var res = System.Windows.Media.ColorConverter.ConvertFromString(hex);
            if (res is Color c) return c;
        }
        catch { }
        return fallback;
    }

    public static SolidColorBrush ParseBrush(string? hex, SolidColorBrush fallback)
    {
        return new SolidColorBrush(ParseColor(hex, fallback.Color));
    }

    public static string ColorToHex(Color color, bool includeAlpha = false)
    {
        return includeAlpha 
            ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static string SanitizeFileName(string name)
    {
        var invalids = Path.GetInvalidFileNameChars();
        var chars = name.Where(c => !invalids.Contains(c) && !char.IsWhiteSpace(c)).ToArray();
        string clean = new string(chars).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(clean) ? "custom_theme" : clean;
    }
}
