using System.Windows;

namespace WpfApp3.Services;

public sealed class ThemeService
{
    public const string DarkThemeName = "dark";
    public const string LightThemeName = "light";

    private const string ThemeDictionaryMarkerKey = "AppThemeMarkerBrush";

    private static readonly IReadOnlyDictionary<string, string> ThemeUris = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [DarkThemeName] = "Themes/Theme.Dark.xaml",
        [LightThemeName] = "Themes/Theme.Light.xaml"
    };

    private ThemeService()
    {
    }

    public static ThemeService Instance { get; } = new();

    public event EventHandler? ThemeChanged;

    public string CurrentThemeName { get; private set; } = DarkThemeName;

    public void ApplyTheme(string? themeName)
    {
        string normalizedThemeName = NormalizeThemeName(themeName);
        CurrentThemeName = normalizedThemeName;

        if (System.Windows.Application.Current is null)
        {
            return;
        }

        ResourceDictionary themeDictionary = new()
        {
            Source = new Uri(ThemeUris[normalizedThemeName], UriKind.Relative)
        };

        IList<ResourceDictionary> mergedDictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;

        for (int index = mergedDictionaries.Count - 1; index >= 0; index--)
        {
            if (mergedDictionaries[index].Contains(ThemeDictionaryMarkerKey))
            {
                mergedDictionaries.RemoveAt(index);
            }
        }

        mergedDictionaries.Insert(0, themeDictionary);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public static string NormalizeThemeName(string? themeName)
    {
        return string.Equals(themeName, LightThemeName, StringComparison.OrdinalIgnoreCase)
            ? LightThemeName
            : DarkThemeName;
    }
}