using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using WpfApp3.Services;

namespace WpfApp3.Pages;

public partial class HelpPage : Page
{
    private readonly WorkspacePage? _workspacePage;
    private readonly Action? _showWorkspacePage;
    private int _currentStep = 1;

    private enum QuickStartScenario
    {
        FolderWorkspace,
        EncryptedStorage,
        SingleFile
    }

    public HelpPage()
        : this(null, null)
    {
    }

    public HelpPage(WorkspacePage? workspacePage, Action? showWorkspacePage)
    {
        _workspacePage = workspacePage;
        _showWorkspacePage = showWorkspacePage;

        InitializeComponent();
        Loaded += HelpPage_Loaded;
        Unloaded += HelpPage_Unloaded;
        ResetState();
    }

    private void HelpPage_Loaded(object sender, RoutedEventArgs e)
    {
        LocalizationService.Instance.CultureChanged += LocalizationService_CultureChanged;
        ApplyLocalization();
        UpdateScenarioPanels();
        UpdateWizardState();
    }

    private void HelpPage_Unloaded(object sender, RoutedEventArgs e)
    {
        LocalizationService.Instance.CultureChanged -= LocalizationService_CultureChanged;
    }

    private void LocalizationService_CultureChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
        UpdateScenarioPanels();
        UpdateWizardState();
    }

    private void ScenarioSelectionChanged(object sender, RoutedEventArgs e)
    {
        HandleConfigurationChanged();
    }

    private void ModeSelectionChanged(object sender, RoutedEventArgs e)
    {
        HandleConfigurationChanged();
    }

    private void InputTextChanged(object sender, TextChangedEventArgs e)
    {
        HandleConfigurationChanged();
    }

    private void InputPasswordChanged(object sender, RoutedEventArgs e)
    {
        HandleConfigurationChanged();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            _currentStep--;
            SetValidation(null);
            UpdateWizardState();
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 1)
        {
            _currentStep = 2;
            SetValidation(null);
            UpdateWizardState();
            return;
        }

        if (!ValidateConfiguration(out string message))
        {
            SetValidation(message);
            return;
        }

        _currentStep = 3;
        SetValidation(null);
        UpdateWizardState();
    }

    private void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateConfiguration(out string message))
        {
            _currentStep = 2;
            SetValidation(message);
            UpdateWizardState();
            return;
        }

        if (_workspacePage is null)
        {
            SetValidation(T("Рабочее пространство сейчас недоступно.", "The workspace is not available right now."));
            return;
        }

        bool success = ExecuteScenario();

        if (success)
        {
            SetValidation(null);
            _showWorkspacePage?.Invoke();
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ResetState();
        ApplyLocalization();
        UpdateScenarioPanels();
        UpdateWizardState();
    }

    private void FolderBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using Forms.FolderBrowserDialog dialog = new()
        {
            Description = T("Выберите папку хранилища", "Choose the workspace folder."),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = GetInitialDirectory(FolderPathTextBox.Text)
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            FolderPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void EncryptedBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsCreateEncryptedMode())
        {
            Microsoft.Win32.SaveFileDialog dialog = new()
            {
                Filter = "Encrypted storages (*.wstore)|*.wstore",
                DefaultExt = ".wstore",
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = GetInitialDirectory(EncryptedPathTextBox.Text),
                FileName = GetFileNameOrDefault(EncryptedPathTextBox.Text, "secure-storage.wstore"),
                Title = T("Создать зашифрованное хранилище", "Create encrypted storage")
            };

            if (dialog.ShowDialog() == true)
            {
                EncryptedPathTextBox.Text = dialog.FileName;
            }

            return;
        }

        Microsoft.Win32.OpenFileDialog openDialog = new()
        {
            CheckFileExists = true,
            Filter = "Encrypted storages (*.wstore)|*.wstore|All files (*.*)|*.*",
            InitialDirectory = GetInitialDirectory(EncryptedPathTextBox.Text),
            Title = T("Открыть зашифрованное хранилище", "Open encrypted storage")
        };

        if (openDialog.ShowDialog() == true)
        {
            EncryptedPathTextBox.Text = openDialog.FileName;
        }
    }

    private void SingleFileBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsCreateSingleFileMode())
        {
            Microsoft.Win32.SaveFileDialog dialog = new()
            {
                Filter = "All files (*.*)|*.*|Text documents (*.txt)|*.txt",
                DefaultExt = ".txt",
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = GetInitialDirectory(SingleFilePathTextBox.Text),
                FileName = GetFileNameOrDefault(SingleFilePathTextBox.Text, "note.txt"),
                Title = T("Создать файл", "Create file")
            };

            if (dialog.ShowDialog() == true)
            {
                SingleFilePathTextBox.Text = dialog.FileName;
            }

            return;
        }

        Microsoft.Win32.OpenFileDialog openDialog = new()
        {
            CheckFileExists = true,
            Filter = "All files (*.*)|*.*|Text documents (*.txt)|*.txt",
            InitialDirectory = GetInitialDirectory(SingleFilePathTextBox.Text),
            Title = T("Открыть файл", "Open file")
        };

        if (openDialog.ShowDialog() == true)
        {
            SingleFilePathTextBox.Text = openDialog.FileName;
        }
    }

    private void HandleConfigurationChanged()
    {
        SetValidation(null);
        UpdateScenarioPanels();

        if (_currentStep == 3)
        {
            UpdateSummary();
        }
    }

    private void ResetState()
    {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        FolderScenarioRadioButton.IsChecked = true;
        OpenEncryptedModeRadioButton.IsChecked = true;
        OpenSingleFileModeRadioButton.IsChecked = true;
        FolderPathTextBox.Text = Path.Combine(documentsPath, "Workspace");
        EncryptedPathTextBox.Text = Path.Combine(documentsPath, "secure-storage.wstore");
        SingleFilePathTextBox.Text = Path.Combine(documentsPath, "note.txt");
        EncryptedPasswordBox.Password = string.Empty;
        EncryptedConfirmPasswordBox.Password = string.Empty;
        _currentStep = 1;
        SetValidation(null);
    }

    private void ApplyLocalization()
    {
        TitleTextBlock.Text = T("\u0411\u044b\u0441\u0442\u0440\u044b\u0439 \u0441\u0442\u0430\u0440\u0442", "Quick Start");
        DescriptionTextBlock.Text = T(
            "Быстрый мастер запуска показывает реальные сценарии приложения через страницы, валидацию и аккуратный переход в рабочее пространство.",
            "The quick start wizard walks through real app scenarios with pages, validation, and a clean handoff into the workspace.");

        Step1IndicatorTextBlock.Text = T("1. Сценарий", "1. Scenario");
        Step2IndicatorTextBlock.Text = T("2. Параметры", "2. Details");
        Step3IndicatorTextBlock.Text = T("3. Запуск", "3. Launch");

        Step1TitleTextBlock.Text = T("С чего начнем?", "Where do we start?");
        Step1DescriptionTextBlock.Text = T(
            "Выберите сценарий, который лучше всего показывает работу приложения. Следующий шаг подстроится автоматически.",
            "Choose the scenario that best demonstrates the app. The next step will adapt automatically.");

        FolderScenarioTitleTextBlock.Text = T("Рабочая папка", "Folder workspace");
        FolderScenarioBodyTextBlock.Text = T("Открыть папку как дерево документов и работать с ней как с основным хранилищем.", "Open a folder as a document tree and use it as the main workspace.");
        EncryptedScenarioTitleTextBlock.Text = T("Зашифрованное хранилище", "Encrypted storage");
        EncryptedScenarioBodyTextBlock.Text = T("Открыть существующее или создать новое защищенное .wstore-хранилище.", "Open an existing protected .wstore storage or create a new one.");
        SingleFileScenarioTitleTextBlock.Text = T("Одиночный файл", "Single file");
        SingleFileScenarioBodyTextBlock.Text = T("Работать с отдельным файлом, не отказываясь от текущего дерева хранилища.", "Work with a standalone file without giving up the current storage tree.");

        Step2TitleTextBlock.Text = T("Параметры", "Details");
        Step2DescriptionTextBlock.Text = T("Укажите путь и, если нужно, выберите режим открытия или создания.", "Provide a path and choose whether to open or create when needed.");

        FolderDetailsTitleTextBlock.Text = T("Рабочая папка", "Folder workspace");
        FolderDetailsHintTextBlock.Text = T("Можно выбрать существующую папку или новую. Если папка еще не создана, приложение создаст ее автоматически.", "You can choose an existing folder or a new one. If it does not exist yet, the app will create it automatically.");
        FolderPathLabelTextBlock.Text = T("Путь к папке", "Folder path");

        EncryptedDetailsTitleTextBlock.Text = T("Зашифрованное хранилище", "Encrypted storage");
        EncryptedDetailsHintTextBlock.Text = T("Этот сценарий использует тот же рабочий процесс архивов .wstore, что и основное окно.", "This scenario uses the same .wstore archive workflow as the main window.");
        EncryptedModeLabelTextBlock.Text = T("Что сделать", "Action");
        OpenEncryptedModeTextBlock.Text = T("Открыть существующее", "Open existing");
        CreateEncryptedModeTextBlock.Text = T("Создать новое", "Create new");
        EncryptedPathLabelTextBlock.Text = T("Путь к хранилищу", "Storage path");
        PasswordLabelTextBlock.Text = T("Пароль", "Password");
        ConfirmPasswordLabelTextBlock.Text = T("Подтверждение пароля", "Confirm password");

        SingleFileDetailsTitleTextBlock.Text = T("Одиночный файл", "Single file");
        SingleFileDetailsHintTextBlock.Text = T("Файл можно открыть как есть или создать новый пустой документ в выбранном месте.", "Open an existing file or create a new empty document in the chosen location.");
        SingleFileModeLabelTextBlock.Text = T("Что сделать", "Action");
        OpenSingleFileModeTextBlock.Text = T("Открыть существующий", "Open existing");
        CreateSingleFileModeTextBlock.Text = T("Создать новый", "Create new");
        SingleFilePathLabelTextBlock.Text = T("Путь к файлу", "File path");
        SingleFileOutsideStorageHintTextBlock.Text = T("Если файл окажется вне текущего дерева, приложение предложит переключиться на папку файла.", "If the file is outside the current tree, the app will offer to switch to the file folder.");

        Step3TitleTextBlock.Text = T("Проверка перед запуском", "Review before launch");
        Step3DescriptionTextBlock.Text = T("Проверьте итоговый сценарий и откройте его в основном рабочем пространстве.", "Review the final scenario and open it in the main workspace.");
        LaunchHintTextBlock.Text = T("После запуска вы вернетесь в основное рабочее пространство приложения.", "After launch, you will return to the main workspace.");

        FolderBrowseButton.Content = T("Обзор...", "Browse...");
        EncryptedBrowseButton.Content = T("Обзор...", "Browse...");
        SingleFileBrowseButton.Content = T("Обзор...", "Browse...");
        ResetButton.Content = T("Сбросить", "Reset");
        BackButton.Content = T("Назад", "Back");
        NextButton.Content = T("Далее", "Next");
        LaunchButton.Content = T("Запустить", "Launch");

        UpdateSummary();
    }

    private void UpdateScenarioPanels()
    {
        QuickStartScenario scenario = GetSelectedScenario();
        FolderDetailsPanel.Visibility = scenario == QuickStartScenario.FolderWorkspace ? Visibility.Visible : Visibility.Collapsed;
        EncryptedDetailsPanel.Visibility = scenario == QuickStartScenario.EncryptedStorage ? Visibility.Visible : Visibility.Collapsed;
        SingleFileDetailsPanel.Visibility = scenario == QuickStartScenario.SingleFile ? Visibility.Visible : Visibility.Collapsed;
        ConfirmPasswordPanel.Visibility = scenario == QuickStartScenario.EncryptedStorage && IsCreateEncryptedMode()
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateWizardState()
    {
        Step1Panel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        BackButton.Visibility = _currentStep == 1 ? Visibility.Collapsed : Visibility.Visible;
        NextButton.Visibility = _currentStep < 3 ? Visibility.Visible : Visibility.Collapsed;
        LaunchButton.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;

        UpdateIndicatorState(Step1IndicatorBorder, Step1IndicatorTextBlock, 1);
        UpdateIndicatorState(Step2IndicatorBorder, Step2IndicatorTextBlock, 2);
        UpdateIndicatorState(Step3IndicatorBorder, Step3IndicatorTextBlock, 3);
        UpdateSummary();
    }

    private void UpdateIndicatorState(Border border, TextBlock textBlock, int step)
    {
        if (_currentStep > step)
        {
            border.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
            border.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
            textBlock.Foreground = (System.Windows.Media.Brush)FindResource("TextInvertedBrush");
            return;
        }

        if (_currentStep == step)
        {
            border.Background = (System.Windows.Media.Brush)FindResource("PanelAltBackgroundBrush");
            border.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
            textBlock.Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush");
            return;
        }

        border.Background = (System.Windows.Media.Brush)FindResource("SurfaceBackgroundBrush");
        border.BorderBrush = (System.Windows.Media.Brush)FindResource("ControlBorderBrush");
        textBlock.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
    }

    private void UpdateSummary()
    {
        SummaryTitleTextBlock.Text = GetSummaryTitle();
        SummaryActionTextBlock.Text = $"{T("Действие", "Action")}: {GetSummaryAction()}";
        SummaryPathTextBlock.Text = $"{T("Путь", "Path")}: {GetSummaryPath()}";
        SummaryNoteTextBlock.Text = $"{T("Заметка", "Note")}: {GetSummaryNote()}";
    }

    private bool ValidateConfiguration(out string message)
    {
        message = string.Empty;

        switch (GetSelectedScenario())
        {
            case QuickStartScenario.FolderWorkspace:
                if (!TryGetFullPath(FolderPathTextBox.Text, out string folderPath))
                {
                    message = T("Укажите корректный путь к папке.", "Enter a valid folder path.");
                    return false;
                }

                if (File.Exists(folderPath))
                {
                    message = T("Указанный путь ведет к файлу, а не к папке.", "The selected path points to a file, not a folder.");
                    return false;
                }

                if (!HasAvailableRoot(folderPath))
                {
                    message = T("Корневой путь для этой папки сейчас недоступен.", "The root path for this folder is not available right now.");
                    return false;
                }

                return true;

            case QuickStartScenario.EncryptedStorage:
                if (!TryGetArchivePath(EncryptedPathTextBox.Text, out string archivePath))
                {
                    message = T("Укажите корректный путь к хранилищу.", "Enter a valid storage path.");
                    return false;
                }

                if (!HasAvailableRoot(archivePath))
                {
                    message = T("Корневой путь для этого хранилища сейчас недоступен.", "The root path for this storage is not available right now.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(EncryptedPasswordBox.Password))
                {
                    message = T("Введите пароль.", "Enter a password.");
                    return false;
                }

                if (IsCreateEncryptedMode())
                {
                    if (EncryptedPasswordBox.Password != EncryptedConfirmPasswordBox.Password)
                    {
                        message = T("Пароли не совпадают.", "Passwords do not match.");
                        return false;
                    }

                    if (File.Exists(archivePath) || Directory.Exists(archivePath))
                    {
                        message = T("Файл хранилища уже существует. Выберите другое имя.", "The storage file already exists. Choose another name.");
                        return false;
                    }

                    return true;
                }

                if (!File.Exists(archivePath))
                {
                    message = T("Указанное хранилище не найдено.", "The selected storage was not found.");
                    return false;
                }

                return true;

            default:
                if (!TryGetFullPath(SingleFilePathTextBox.Text, out string filePath))
                {
                    message = T("Укажите корректный путь к файлу.", "Enter a valid file path.");
                    return false;
                }

                if (!HasAvailableRoot(filePath))
                {
                    message = T("Корневой путь для этого файла сейчас недоступен.", "The root path for this file is not available right now.");
                    return false;
                }

                if (IsCreateSingleFileMode())
                {
                    if (File.Exists(filePath) || Directory.Exists(filePath))
                    {
                        message = T("Файл уже существует. Выберите другое имя.", "The file already exists. Choose another name.");
                        return false;
                    }

                    return true;
                }

                if (!File.Exists(filePath))
                {
                    message = T("Указанный файл не найден.", "The selected file was not found.");
                    return false;
                }

                return true;
        }
    }

    private bool ExecuteScenario()
    {
        return GetSelectedScenario() switch
        {
            QuickStartScenario.FolderWorkspace => TryGetFullPath(FolderPathTextBox.Text, out string folderPath)
                && _workspacePage!.OpenStorageFolder(folderPath),
            QuickStartScenario.EncryptedStorage => TryGetArchivePath(EncryptedPathTextBox.Text, out string archivePath)
                && (IsCreateEncryptedMode()
                    ? _workspacePage!.CreateEncryptedStorage(archivePath, EncryptedPasswordBox.Password)
                    : _workspacePage!.OpenEncryptedStorage(archivePath, EncryptedPasswordBox.Password)),
            _ => TryGetFullPath(SingleFilePathTextBox.Text, out string filePath)
                && (IsCreateSingleFileMode()
                    ? _workspacePage!.CreateDocumentFile(filePath)
                    : _workspacePage!.OpenDocumentFile(filePath))
        };
    }

    private QuickStartScenario GetSelectedScenario()
    {
        if (EncryptedScenarioRadioButton.IsChecked == true)
        {
            return QuickStartScenario.EncryptedStorage;
        }

        if (SingleFileScenarioRadioButton.IsChecked == true)
        {
            return QuickStartScenario.SingleFile;
        }

        return QuickStartScenario.FolderWorkspace;
    }

    private bool IsCreateEncryptedMode()
    {
        return CreateEncryptedModeRadioButton.IsChecked == true;
    }

    private bool IsCreateSingleFileMode()
    {
        return CreateSingleFileModeRadioButton.IsChecked == true;
    }

    private static bool TryGetFullPath(string? value, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(value.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetArchivePath(string? value, out string archivePath)
    {
        archivePath = string.Empty;

        if (!TryGetFullPath(value, out string fullPath))
        {
            return false;
        }

        archivePath = Path.GetExtension(fullPath).Equals(".wstore", StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath + ".wstore";
        return true;
    }

    private static bool HasAvailableRoot(string path)
    {
        string? root = Path.GetPathRoot(path);
        return !string.IsNullOrWhiteSpace(root) && Directory.Exists(root);
    }

    private static string GetInitialDirectory(string? currentPath)
    {
        if (TryGetFullPath(currentPath, out string fullPath))
        {
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }

            string? directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private static string GetFileNameOrDefault(string? currentPath, string fallback)
    {
        if (TryGetFullPath(currentPath, out string fullPath))
        {
            string fileName = Path.GetFileName(fullPath);

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return fallback;
    }

    private string GetSummaryTitle()
    {
        return GetSelectedScenario() switch
        {
            QuickStartScenario.FolderWorkspace => T("Рабочая папка", "Folder workspace"),
            QuickStartScenario.EncryptedStorage when IsCreateEncryptedMode() => T("Создание зашифрованного хранилища", "Create encrypted storage"),
            QuickStartScenario.EncryptedStorage => T("Открытие зашифрованного хранилища", "Open encrypted storage"),
            QuickStartScenario.SingleFile when IsCreateSingleFileMode() => T("Создание файла", "Create file"),
            _ => T("Открытие файла", "Open file")
        };
    }

    private string GetSummaryAction()
    {
        return GetSelectedScenario() switch
        {
            QuickStartScenario.FolderWorkspace => T("Открыть папку как основное рабочее пространство.", "Open the folder as the main workspace."),
            QuickStartScenario.EncryptedStorage when IsCreateEncryptedMode() => T("Создать новое .wstore-хранилище и сразу открыть его.", "Create a new .wstore storage and open it immediately."),
            QuickStartScenario.EncryptedStorage => T("Разблокировать и открыть существующее .wstore-хранилище.", "Unlock and open the existing .wstore storage."),
            QuickStartScenario.SingleFile when IsCreateSingleFileMode() => T("Создать пустой файл и открыть его в редакторе.", "Create an empty file and open it in the editor."),
            _ => T("Открыть существующий файл в редакторе.", "Open the existing file in the editor.")
        };
    }

    private string GetSummaryPath()
    {
        return GetSelectedScenario() switch
        {
            QuickStartScenario.FolderWorkspace when TryGetFullPath(FolderPathTextBox.Text, out string folderPath) => folderPath,
            QuickStartScenario.EncryptedStorage when TryGetArchivePath(EncryptedPathTextBox.Text, out string archivePath) => archivePath,
            QuickStartScenario.SingleFile when TryGetFullPath(SingleFilePathTextBox.Text, out string filePath) => filePath,
            QuickStartScenario.FolderWorkspace => FolderPathTextBox.Text.Trim(),
            QuickStartScenario.EncryptedStorage => EncryptedPathTextBox.Text.Trim(),
            _ => SingleFilePathTextBox.Text.Trim()
        };
    }

    private string GetSummaryNote()
    {
        return GetSelectedScenario() switch
        {
            QuickStartScenario.FolderWorkspace => T("Папка будет создана автоматически, если ее еще нет.", "The folder will be created automatically if it does not exist yet."),
            QuickStartScenario.EncryptedStorage when IsCreateEncryptedMode() => T("Расширение .wstore будет добавлено автоматически, если вы его не указали.", "The .wstore extension will be added automatically if you omit it."),
            QuickStartScenario.EncryptedStorage => T("Для открытия понадобится корректный пароль от выбранного хранилища.", "You will need the correct password for the selected storage."),
            _ => T("Если файл находится вне текущего дерева, приложение предложит переключиться на папку файла.", "If the file is outside the current tree, the app will offer to switch to its folder.")
        };
    }

    private void SetValidation(string? message)
    {
        ValidationTextBlock.Text = message ?? string.Empty;
        ValidationTextBlock.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private string T(string russianText, string englishText)
    {
        return LocalizationService.Instance.CurrentCulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase) ? russianText : englishText;
    }
}
