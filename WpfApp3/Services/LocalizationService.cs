using System.ComponentModel;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Resources;
using WpfApp3.Models;

namespace WpfApp3.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private const string DefaultCultureName = "ru";
    private const string ResourceBaseName = "WpfApp3.Strings";
    private const string ResourceSuffix = ".resources";

    private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
    private readonly ResourceManager _resourceManager;
    private readonly HashSet<string> _supportedCultureNames;
    private readonly IReadOnlyList<CultureInfo> _availableCultures;
    private readonly IReadOnlyList<LanguageOption> _availableLanguages;
    private CultureInfo _currentCulture;

    private LocalizationService()
    {
        _resourceManager = new ResourceManager(ResourceBaseName, _assembly);
        _supportedCultureNames = DiscoverSupportedCultureNames(_assembly);
        _availableCultures = CreateAvailableCultures(_supportedCultureNames);
        _availableLanguages = _availableCultures.Select(CreateLanguageOption).ToArray();
        _currentCulture = ResolveCulture(DefaultCultureName);
    }

    public static LocalizationService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CultureChanged;

    public CultureInfo CurrentCulture => _currentCulture;

    public IReadOnlyList<CultureInfo> AvailableCultures => _availableCultures;

    public IReadOnlyList<LanguageOption> AvailableLanguages => _availableLanguages;

    public string this[string key] => _resourceManager.GetString(key, _currentCulture) ?? key;

    public void SetCulture(string cultureName)
    {
        CultureInfo culture = ResolveCulture(cultureName);
        bool changed = !_currentCulture.Name.Equals(culture.Name, StringComparison.OrdinalIgnoreCase);

        _currentCulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        if (!changed)
        {
            return;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyList<CultureInfo> CreateAvailableCultures(IEnumerable<string> cultureNames)
    {
        return cultureNames
            .Select(CultureInfo.GetCultureInfo)
            .OrderBy(culture => culture.Name.Equals(DefaultCultureName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(GetLanguageDisplayName, StringComparer.InvariantCultureIgnoreCase)
            .ToArray();
    }

    private static LanguageOption CreateLanguageOption(CultureInfo culture)
    {
        return new(culture.Name, GetLanguageDisplayName(culture));
    }

    private static string GetLanguageDisplayName(CultureInfo culture)
    {
        string displayName = string.IsNullOrWhiteSpace(culture.NativeName)
            ? culture.DisplayName
            : culture.NativeName;

        return culture.TextInfo.ToTitleCase(displayName);
    }

    private static HashSet<string> DiscoverSupportedCultureNames(Assembly assembly)
    {
        HashSet<string> supportedCultureNames = new(StringComparer.OrdinalIgnoreCase)
        {
            DefaultCultureName
        };

        AddCulturesFromAssemblyResources(supportedCultureNames, assembly);
        AddCulturesFromSatelliteAssemblies(supportedCultureNames, assembly);

        return supportedCultureNames;
    }

    private static void AddCulturesFromAssemblyResources(ISet<string> supportedCultureNames, Assembly assembly)
    {
        foreach (string resourceName in assembly.GetManifestResourceNames())
        {
            if (!TryGetCultureNameFromResource(resourceName, out string? cultureName))
            {
                continue;
            }

            supportedCultureNames.Add(cultureName);
        }
    }

    private static void AddCulturesFromSatelliteAssemblies(ISet<string> supportedCultureNames, Assembly assembly)
    {
        string baseDirectory = AppContext.BaseDirectory;

        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
        {
            return;
        }

        foreach (string directoryPath in Directory.EnumerateDirectories(baseDirectory))
        {
            string? directoryName = Path.GetFileName(directoryPath);

            if (!TryGetCultureInfo(directoryName, out CultureInfo culture))
            {
                continue;
            }

            try
            {
                Assembly satelliteAssembly = assembly.GetSatelliteAssembly(culture);
                AddCulturesFromAssemblyResources(supportedCultureNames, satelliteAssembly);
            }
            catch (FileNotFoundException)
            {
            }
            catch (FileLoadException)
            {
            }
            catch (BadImageFormatException)
            {
            }
        }
    }

    private static bool TryGetCultureNameFromResource(string resourceName, out string cultureName)
    {
        cultureName = string.Empty;
        string resourcePrefix = $"{ResourceBaseName}.";

        if (!resourceName.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase) ||
            !resourceName.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int cultureLength = resourceName.Length - resourcePrefix.Length - ResourceSuffix.Length;

        if (cultureLength <= 0)
        {
            return false;
        }

        string candidateCultureName = resourceName.Substring(resourcePrefix.Length, cultureLength);

        if (!TryGetCultureInfo(candidateCultureName, out CultureInfo culture))
        {
            return false;
        }

        cultureName = culture.Name;
        return true;
    }

    private static bool TryGetCultureInfo(string? cultureName, out CultureInfo culture)
    {
        culture = CultureInfo.GetCultureInfo(DefaultCultureName);

        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return false;
        }

        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private CultureInfo ResolveCulture(string? cultureName)
    {
        if (TryResolveSupportedCulture(cultureName, out CultureInfo culture))
        {
            return culture;
        }

        return CultureInfo.GetCultureInfo(DefaultCultureName);
    }

    private bool TryResolveSupportedCulture(string? cultureName, out CultureInfo culture)
    {
        culture = CultureInfo.GetCultureInfo(DefaultCultureName);

        if (!TryGetCultureInfo(cultureName, out CultureInfo requestedCulture))
        {
            return false;
        }

        if (_supportedCultureNames.Contains(requestedCulture.Name))
        {
            culture = requestedCulture;
            return true;
        }

        for (CultureInfo parent = requestedCulture.Parent; !string.IsNullOrWhiteSpace(parent.Name); parent = parent.Parent)
        {
            if (_supportedCultureNames.Contains(parent.Name))
            {
                culture = CultureInfo.GetCultureInfo(parent.Name);
                return true;
            }
        }

        return false;
    }
}