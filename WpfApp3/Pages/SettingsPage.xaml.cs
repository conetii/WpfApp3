using System.Windows;
using System.Windows.Controls;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.Pages;

public partial class SettingsPage : Page
{
    private bool _isSyncing;
    private StorageReference? _storageReference;

    public SettingsPage(IEnumerable<LanguageOption> languages)
    {
        InitializeComponent();
        LanguageComboBox.ItemsSource = languages.ToList();
        Loaded += SettingsPage_Loaded;
        Unloaded += SettingsPage_Unloaded;
        ApplyLocalization();
    }

    public event EventHandler<string>? LanguageChangedRequested;

    public void SetSelectedLanguage(string cultureName)
    {
        _isSyncing = true;
        LanguageComboBox.SelectedValue = cultureName;
        _isSyncing = false;
    }

    public void UpdateStorageReference(StorageReference? storageReference)
    {
        _storageReference = storageReference;
        StorageValueTextBlock.Text = GetStorageDisplayText();
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        LocalizationService.Instance.CultureChanged += LocalizationService_CultureChanged;
        ApplyLocalization();
    }

    private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        LocalizationService.Instance.CultureChanged -= LocalizationService_CultureChanged;
    }

    private void LocalizationService_CultureChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing || LanguageComboBox.SelectedItem is not LanguageOption option)
        {
            return;
        }

        LanguageChangedRequested?.Invoke(this, option.CultureName);
    }

    private void ApplyLocalization()
    {
        TitleTextBlock.Text = Localize("SettingsTitle");
        DescriptionTextBlock.Text = Localize("SettingsDescription");
        LanguageLabelTextBlock.Text = Localize("LanguageLabel");
        StorageLabelTextBlock.Text = Localize("SettingsStorageLabel");
        StorageValueTextBlock.Text = GetStorageDisplayText();
    }

    private string GetStorageDisplayText()
    {
        return _storageReference is null
            ? Localize("NoStorageSelected")
            : _storageReference.SourcePath;
    }

    private static string Localize(string key)
    {
        return LocalizationService.Instance[key];
    }
}