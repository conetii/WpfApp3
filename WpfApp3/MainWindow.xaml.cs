using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Navigation;
using WpfApp3.Models;
using WpfApp3.Pages;
using WpfApp3.Services;

namespace WpfApp3;

public partial class MainWindow : Window
{
    private readonly AppSettingsService _settingsService = new();
    private readonly AppSettings _settings;
    private readonly WorkspacePage _workspacePage;
    private readonly SettingsPage _settingsPage;
    private readonly HelpPage _helpPage;

    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int attributeValue, int attributeSize);

    public MainWindow()
    {
        _settings = _settingsService.Load();
        LocalizationService.Instance.SetCulture(string.IsNullOrWhiteSpace(_settings.Language) ? "ru" : _settings.Language);
        ThemeService.Instance.ApplyTheme(_settings.Theme);

        InitializeComponent();

        StorageReference? initialStorageReference = CreateInitialStorageReference();
        _workspacePage = new WorkspacePage(initialStorageReference);
        _settingsPage = new SettingsPage(LocalizationService.Instance.AvailableLanguages);
        _helpPage = new HelpPage(_workspacePage, ShowWorkspacePage);

        _workspacePage.StorageReferenceChanged += WorkspacePage_StorageReferenceChanged;
        _workspacePage.StateChanged += WorkspacePage_StateChanged;
        _settingsPage.LanguageChangedRequested += SettingsPage_LanguageChangedRequested;
        MainFrame.Navigated += MainFrame_Navigated;
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        StateChanged += MainWindow_StateChanged;
        SourceInitialized += MainWindow_SourceInitialized;
        LocalizationService.Instance.CultureChanged += LocalizationService_CultureChanged;
        ThemeService.Instance.ThemeChanged += ThemeService_ThemeChanged;

        _settingsPage.SetSelectedLanguage(LocalizationService.Instance.CurrentCulture.Name);
        _settingsPage.UpdateStorageReference(initialStorageReference);
        ApplyLocalization();
        UpdateNavigationButtons();
        UpdateStorageMenuItems();
        UpdateCaptionButtons();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ShowWorkspacePage();
        UpdateNavigationButtons();
        UpdateStorageMenuItems();
        UpdateCaptionButtons();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        nint handle = new WindowInteropHelper(this).Handle;

        if (handle == nint.Zero)
        {
            return;
        }

        int preference = DwmwcpRound;
        DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        LocalizationService.Instance.CultureChanged -= LocalizationService_CultureChanged;
        ThemeService.Instance.ThemeChanged -= ThemeService_ThemeChanged;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdateCaptionButtons();
    }

    private void ThemeService_ThemeChanged(object? sender, EventArgs e)
    {
        RefreshThemeMenuItems();
        UpdateCaptionButtons();
    }

    private void LocalizationService_CultureChanged(object? sender, EventArgs e)
    {
        _settingsPage.SetSelectedLanguage(LocalizationService.Instance.CurrentCulture.Name);
        ApplyLocalization();
        UpdateNavigationButtons();
        UpdateStorageMenuItems();
        UpdateCaptionButtons();
    }

    private void WorkspacePage_StorageReferenceChanged(object? sender, StorageReference? storageReference)
    {
        _settings.LastStoragePath = storageReference?.SourcePath;
        _settings.LastStorageKind = storageReference?.Kind ?? StorageKind.Folder;
        _settings.StorageFolderPath = storageReference?.Kind == StorageKind.Folder ? storageReference.SourcePath : null;
        SaveSettings();
        _settingsPage.UpdateStorageReference(storageReference);
    }

    private void WorkspacePage_StateChanged(object? sender, EventArgs e)
    {
        CommandManager.InvalidateRequerySuggested();
        UpdateStorageMenuItems();
    }

    private void SettingsPage_LanguageChangedRequested(object? sender, string cultureName)
    {
        ApplyLanguageSelection(cultureName);
    }

    private void MainFrame_Navigated(object? sender, NavigationEventArgs e)
    {
        CommandManager.InvalidateRequerySuggested();
        UpdateNavigationButtons();
        UpdateStorageMenuItems();
    }

    private void WorkspaceCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = true;
        e.Handled = true;
    }

    private void DeleteDocumentCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _workspacePage.CanDeleteCurrentDocument;
        e.Handled = true;
    }

    private void UndoCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ReferenceEquals(MainFrame.Content, _workspacePage) && _workspacePage.CanUndoEdit;
        e.Handled = true;
    }

    private void RedoCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ReferenceEquals(MainFrame.Content, _workspacePage) && _workspacePage.CanRedoEdit;
        e.Handled = true;
    }

    private void FindCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = true;
        e.Handled = true;
    }

    private void NewCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ShowWorkspacePage();
        _workspacePage.NewDocument();
    }

    private void OpenCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ShowWorkspacePage();
        _workspacePage.ChooseStorageFolder();
    }

    private void OpenFileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ShowWorkspacePage();
        _workspacePage.OpenDocumentFile();
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ShowWorkspacePage();
        _workspacePage.SaveCurrentDocument();
    }

    private void SaveAsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ShowWorkspacePage();
        _workspacePage.SaveCurrentDocumentAs();
    }

    private void CloseDocumentCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ShowWorkspacePage();
        _workspacePage.CloseCurrentDocument();
    }

    private void DeleteDocumentCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ShowWorkspacePage();
        _workspacePage.DeleteCurrentDocument();
    }

    private void UndoCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        _workspacePage.UndoEdit();
    }

    private void RedoCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        _workspacePage.RedoEdit();
    }

    private void FindCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ShowWorkspacePage();
        _workspacePage.FocusSearch();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (MainFrame.CanGoBack)
        {
            MainFrame.GoBack();
        }

        UpdateNavigationButtons();
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (MainFrame.CanGoForward)
        {
            MainFrame.GoForward();
        }

        UpdateNavigationButtons();
    }

    private void WorkspaceHomeButton_Click(object sender, RoutedEventArgs e)
    {
        ShowWorkspacePage();
    }

    private void MinimizeCaptionButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreCaptionButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseCaptionButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenEncryptedStorageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowWorkspacePage();
        _workspacePage.OpenEncryptedStorage();
    }

    private void CreateEncryptedStorageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowWorkspacePage();
        _workspacePage.CreateEncryptedStorage();
    }

    private void UnlockStorageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowWorkspacePage();
        _workspacePage.UnlockCurrentStorage();
    }

    private void CloseStorageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowWorkspacePage();
        _workspacePage.CloseCurrentStorage();
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MainFrame.Navigate(_settingsPage);
    }

    private void ClearSearchMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowWorkspacePage();
        _workspacePage.ClearSearch();
    }

    private void OpenHelpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MainFrame.Navigate(_helpPage);
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LanguageSelectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string cultureName })
        {
            ApplyLanguageSelection(cultureName);
        }
    }

    private void ThemeSelectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string themeName })
        {
            ApplyThemeSelection(themeName);
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (!ReferenceEquals(MainFrame.Content, _workspacePage))
        {
            return;
        }

        if (_workspacePage.TryHandleEscape())
        {
            e.Handled = true;
            return;
        }

        if (_workspacePage.CloseCurrentDocument())
        {
            e.Handled = true;
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_workspacePage.TryCloseApplication())
        {
            e.Cancel = true;
        }
    }

    private void ShowWorkspacePage()
    {
        if (!ReferenceEquals(MainFrame.Content, _workspacePage))
        {
            MainFrame.Navigate(_workspacePage);
            return;
        }

        UpdateNavigationButtons();
    }

    private void UpdateNavigationButtons()
    {
        BackButton.IsEnabled = MainFrame.CanGoBack;
        ForwardButton.IsEnabled = MainFrame.CanGoForward;
        WorkspaceHomeButton.IsEnabled = MainFrame.Content is not null && !ReferenceEquals(MainFrame.Content, _workspacePage);
    }

    private void UpdateStorageMenuItems()
    {
        if (UnlockStorageMenuItem is null)
        {
            return;
        }

        UnlockStorageMenuItem.IsEnabled = _workspacePage.CanUnlockCurrentStorage;
    }

    private void UpdateCaptionButtons()
    {
        if (MaximizeRestoreCaptionButton is null)
        {
            return;
        }

        MaximizeRestoreCaptionButton.Content = WindowState == WindowState.Maximized ? "\u2750" : "\u25A1";
    }

    private void ApplyLocalization()
    {
        LocalizationService localization = LocalizationService.Instance;
        Title = localization["WindowTitle"];
        BackButton.ToolTip = localization["NavBackTooltip"];
        ForwardButton.ToolTip = localization["NavForwardTooltip"];
        WorkspaceHomeButton.Content = localization["NavWorkspace"];
        WorkspaceHomeButton.ToolTip = localization["NavWorkspace"];
        FileMenuItem.Header = localization["MenuFile"];
        NewMenuItem.Header = localization["MenuNew"];
        OpenFolderMenuItem.Header = localization["MenuOpenFolder"];
        OpenFileMenuItem.Header = LocalizeOrFallback("MenuOpenFile", "Open file");
        OpenEncryptedStorageMenuItem.Header = localization["MenuOpenEncryptedStorage"];
        UnlockStorageMenuItem.Header = localization["MenuUnlockStorage"];
        CreateEncryptedStorageMenuItem.Header = localization["MenuCreateEncryptedStorage"];
        SaveMenuItem.Header = localization["MenuSave"];
        SaveAsMenuItem.Header = LocalizeOrFallback("MenuSaveAs", "Save as");
        CloseDocumentMenuItem.Header = localization["MenuCloseDocument"];
        DeleteDocumentMenuItem.Header = localization["MenuDeleteDocument"];
        CloseStorageMenuItem.Header = localization["MenuCloseStorage"];
        SettingsMenuItem.Header = localization["MenuSettings"];
        ExitMenuItem.Header = localization["MenuExit"];
        EditMenuItem.Header = localization["MenuEdit"];
        UndoMenuItem.Header = localization["MenuUndo"];
        RedoMenuItem.Header = localization["MenuRedo"];
        SearchMenuItem.Header = localization["MenuSearch"];
        FindMenuItem.Header = localization["MenuFind"];
        ClearSearchMenuItem.Header = localization["MenuClearSearch"];
        HelpMenuItem.Header = LocalizeOrFallback("MenuQuickStart", CultureAwareFallback("Быстрый старт", "Quick Start"));
        OpenHelpMenuItem.Header = LocalizeOrFallback("MenuQuickStartOpen", CultureAwareFallback("Открыть быстрый старт", "Open Quick Start"));
        RefreshLanguageMenuItems();
        RefreshThemeMenuItems();
    }

    private void RefreshLanguageMenuItems()
    {
        if (LanguageMenuItem is null)
        {
            return;
        }

        IReadOnlyList<LanguageOption> languages = LocalizationService.Instance.AvailableLanguages;
        string currentCultureName = LocalizationService.Instance.CurrentCulture.Name;
        string currentDisplayName = languages.FirstOrDefault(language => string.Equals(language.CultureName, currentCultureName, StringComparison.OrdinalIgnoreCase))?.DisplayName
            ?? currentCultureName;

        LanguageMenuItem.Header = $"{LocalizeOrFallback("LanguageLabel", "Language")}: {currentDisplayName}";
        LanguageMenuItem.Items.Clear();

        Style? itemStyle = FindResource("DropDownMenuItemStyle") as Style;

        foreach (LanguageOption language in languages)
        {
            MenuItem item = new()
            {
                Header = BuildMenuChoiceHeader(language.DisplayName, string.Equals(language.CultureName, currentCultureName, StringComparison.OrdinalIgnoreCase)),
                Tag = language.CultureName,
                Style = itemStyle
            };

            item.Click += LanguageSelectionMenuItem_Click;
            LanguageMenuItem.Items.Add(item);
        }
    }

    private void RefreshThemeMenuItems()
    {
        if (ThemeMenuItem is null)
        {
            return;
        }

        IReadOnlyList<ThemeOption> themes = GetThemeOptions();
        string currentThemeName = ThemeService.Instance.CurrentThemeName;
        string currentDisplayName = themes.FirstOrDefault(theme => string.Equals(theme.ThemeName, currentThemeName, StringComparison.OrdinalIgnoreCase))?.DisplayName
            ?? currentThemeName;

        ThemeMenuItem.Header = $"{LocalizeOrFallback("ThemeLabel", "Theme")}: {currentDisplayName}";
        ThemeMenuItem.Items.Clear();

        Style? itemStyle = FindResource("DropDownMenuItemStyle") as Style;

        foreach (ThemeOption theme in themes)
        {
            MenuItem item = new()
            {
                Header = BuildMenuChoiceHeader(theme.DisplayName, string.Equals(theme.ThemeName, currentThemeName, StringComparison.OrdinalIgnoreCase)),
                Tag = theme.ThemeName,
                Style = itemStyle
            };

            item.Click += ThemeSelectionMenuItem_Click;
            ThemeMenuItem.Items.Add(item);
        }
    }

    private IReadOnlyList<ThemeOption> GetThemeOptions()
    {
        return
        [
            new ThemeOption(ThemeService.DarkThemeName, LocalizeOrFallback("ThemeDark", "Dark")),
            new ThemeOption(ThemeService.LightThemeName, LocalizeOrFallback("ThemeLight", "Light"))
        ];
    }

    private void ApplyLanguageSelection(string cultureName)
    {
        _settings.Language = cultureName;
        SaveSettings();
        LocalizationService.Instance.SetCulture(cultureName);
    }

    private void ApplyThemeSelection(string themeName)
    {
        _settings.Theme = ThemeService.NormalizeThemeName(themeName);
        SaveSettings();
        ThemeService.Instance.ApplyTheme(_settings.Theme);
    }

    private string LocalizeOrFallback(string key, string fallback)
    {
        string localized = LocalizationService.Instance[key];
        return string.Equals(localized, key, StringComparison.Ordinal) ? fallback : localized;
    }

    private static string BuildMenuChoiceHeader(string displayName, bool isSelected)
    {
        return isSelected ? $"* {displayName}" : displayName;
    }

    private static string CultureAwareFallback(string russianText, string englishText)
    {
        return LocalizationService.Instance.CurrentCulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase)
            ? russianText
            : englishText;
    }

    private StorageReference? CreateInitialStorageReference()
    {
        if (!string.IsNullOrWhiteSpace(_settings.LastStoragePath))
        {
            return new StorageReference(_settings.LastStorageKind, _settings.LastStoragePath);
        }

        if (!string.IsNullOrWhiteSpace(_settings.StorageFolderPath))
        {
            return new StorageReference(StorageKind.Folder, _settings.StorageFolderPath);
        }

        return null;
    }

    private void SaveSettings()
    {
        _settingsService.Save(_settings);
    }
}