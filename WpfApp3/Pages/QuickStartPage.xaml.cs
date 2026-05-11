using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace WpfApp3.Pages;

public partial class QuickStartPage : Page, INotifyPropertyChanged
{
    private const string DefaultEncryptedFileName = "workspace.wstore";
    private const string DefaultDocumentFileName = "notes.txt";

    private int _currentStep = 1;
    private QuickStartScenario _selectedScenario = QuickStartScenario.FolderWorkspace;
    private QuickStartMode _encryptedMode = QuickStartMode.OpenExisting;
    private QuickStartMode _singleFileMode = QuickStartMode.OpenExisting;
    private string _folderPath = string.Empty;
    private string _encryptedPath = string.Empty;
    private string _singleFilePath = string.Empty;
    private string _encryptedPassword = string.Empty;
    private string _encryptedConfirmation = string.Empty;
    private string _folderPathError = string.Empty;
    private string _encryptedPathError = string.Empty;
    private string _encryptedPasswordError = string.Empty;
    private string _encryptedConfirmationError = string.Empty;
    private string _singleFilePathError = string.Empty;

    public QuickStartPage()
    {
        InitializeComponent();
        DataContext = this;
        RefreshState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<QuickStartRequestEventArgs>? QuickStartRequested;

    public int CurrentStep => _currentStep;

    public bool IsStep1Complete => _currentStep > 1;

    public bool IsStep2Complete => _currentStep > 2;

    public Visibility Step1Visibility => ToVisibility(_currentStep == 1);

    public Visibility Step2Visibility => ToVisibility(_currentStep == 2);

    public Visibility Step3Visibility => ToVisibility(_currentStep == 3);

    public bool IsFolderScenarioSelected
    {
        get => _selectedScenario == QuickStartScenario.FolderWorkspace;
        set
        {
            if (value)
            {
                SetScenario(QuickStartScenario.FolderWorkspace);
            }
        }
    }

    public bool IsEncryptedScenarioSelected
    {
        get => _selectedScenario == QuickStartScenario.EncryptedStorage;
        set
        {
            if (value)
            {
                SetScenario(QuickStartScenario.EncryptedStorage);
            }
        }
    }

    public bool IsSingleFileScenarioSelected
    {
        get => _selectedScenario == QuickStartScenario.SingleFile;
        set
        {
            if (value)
            {
                SetScenario(QuickStartScenario.SingleFile);
            }
        }
    }

    public Visibility FolderScenarioVisibility => ToVisibility(IsFolderScenarioSelected);

    public Visibility EncryptedScenarioVisibility => ToVisibility(IsEncryptedScenarioSelected);

    public Visibility SingleFileScenarioVisibility => ToVisibility(IsSingleFileScenarioSelected);

    public bool IsEncryptedOpenExistingSelected
    {
        get => _encryptedMode == QuickStartMode.OpenExisting;
        set
        {
            if (value)
            {
                SetEncryptedMode(QuickStartMode.OpenExisting);
            }
        }
    }

    public bool IsEncryptedCreateNewSelected
    {
        get => _encryptedMode == QuickStartMode.CreateNew;
        set
        {
            if (value)
            {
                SetEncryptedMode(QuickStartMode.CreateNew);
            }
        }
    }

    public bool IsSingleFileOpenExistingSelected
    {
        get => _singleFileMode == QuickStartMode.OpenExisting;
        set
        {
            if (value)
            {
                SetSingleFileMode(QuickStartMode.OpenExisting);
            }
        }
    }

    public bool IsSingleFileCreateNewSelected
    {
        get => _singleFileMode == QuickStartMode.CreateNew;
        set
        {
            if (value)
            {
                SetSingleFileMode(QuickStartMode.CreateNew);
            }
        }
    }

    public Visibility EncryptedConfirmationVisibility => ToVisibility(IsEncryptedCreateNewSelected);

    public string FolderPath
    {
        get => _folderPath;
        set
        {
            if (SetProperty(ref _folderPath, value))
            {
                RefreshState();
            }
        }
    }

    public string EncryptedPath
    {
        get => _encryptedPath;
        set
        {
            if (SetProperty(ref _encryptedPath, value))
            {
                RefreshState();
            }
        }
    }

    public string SingleFilePath
    {
        get => _singleFilePath;
        set
        {
            if (SetProperty(ref _singleFilePath, value))
            {
                RefreshState();
            }
        }
    }

    public string FolderPathError => _folderPathError;

    public bool HasFolderPathError => !string.IsNullOrWhiteSpace(_folderPathError);

    public Visibility FolderPathErrorVisibility => ToVisibility(HasFolderPathError);

    public string EncryptedPathError => _encryptedPathError;

    public bool HasEncryptedPathError => !string.IsNullOrWhiteSpace(_encryptedPathError);

    public Visibility EncryptedPathErrorVisibility => ToVisibility(HasEncryptedPathError);

    public string EncryptedPasswordError => _encryptedPasswordError;

    public bool HasEncryptedPasswordError => !string.IsNullOrWhiteSpace(_encryptedPasswordError);

    public Visibility EncryptedPasswordErrorVisibility => ToVisibility(HasEncryptedPasswordError);

    public string EncryptedConfirmationError => _encryptedConfirmationError;

    public bool HasEncryptedConfirmationError => !string.IsNullOrWhiteSpace(_encryptedConfirmationError);

    public Visibility EncryptedConfirmationErrorVisibility => ToVisibility(HasEncryptedConfirmationError);

    public string SingleFilePathError => _singleFilePathError;

    public bool HasSingleFilePathError => !string.IsNullOrWhiteSpace(_singleFilePathError);

    public Visibility SingleFilePathErrorVisibility => ToVisibility(HasSingleFilePathError);

    public string Step2Title => _selectedScenario switch
    {
        QuickStartScenario.FolderWorkspace => "Choose the workspace folder",
        QuickStartScenario.EncryptedStorage => "Set up encrypted storage",
        _ => "Point Quick Start at the file"
    };

    public string Step2Description => _selectedScenario switch
    {
        QuickStartScenario.FolderWorkspace => "Use an existing directory that should open as the workspace root.",
        QuickStartScenario.EncryptedStorage => "Choose whether you want to open an existing archive or create a new protected one.",
        _ => "Open an existing file or define a brand-new file path to create."
    };

    public string SelectedScenarioHeadline => _selectedScenario switch
    {
        QuickStartScenario.FolderWorkspace => "Folder workspace keeps the current storage tree front and center.",
        QuickStartScenario.EncryptedStorage => "Encrypted storage is best when the launch needs password-protected content.",
        _ => "Single file gets you straight to one document with minimal ceremony."
    };

    public string SelectedScenarioGuidance => _selectedScenario switch
    {
        QuickStartScenario.FolderWorkspace => "Use this when the session should open around a real directory and expose its files immediately.",
        QuickStartScenario.EncryptedStorage => "Use this when the session should revolve around a `.wstore` archive instead of a plain folder.",
        _ => "Use this when one file matters more than the surrounding workspace structure."
    };
    public string EncryptedDetailsHelpText => IsEncryptedCreateNewSelected
        ? "A new `.wstore` archive will be created at the chosen path and opened with the password you provide here."
        : "Quick Start will hand off an existing encrypted archive path together with the password for this launch.";

    public string SingleFileDetailsHelpText => IsSingleFileCreateNewSelected
        ? "The request will describe a new file path that should be created when the runtime takes over."
        : "The request will point at an existing file so the runtime can open it directly.";

    public bool CanGoBack => _currentStep > 1;

    public bool CanGoNext => _currentStep == 1 || (_currentStep == 2 && IsCurrentConfigurationValid);

    public bool CanLaunch => _currentStep == 3 && IsCurrentConfigurationValid;

    public Visibility NextButtonVisibility => ToVisibility(_currentStep < 3);

    public Visibility LaunchButtonVisibility => ToVisibility(_currentStep == 3);

    public string StepCaption => $"Step {_currentStep} of 3";

    public string FooterHint => _currentStep switch
    {
        1 => "Pick the scenario that matches what you want to open or create.",
        2 => IsCurrentConfigurationValid
            ? "Everything required for this step is ready."
            : "Complete the required fields to continue.",
        _ => "Review the summary and launch the request when you are ready."
    };

    public string LaunchButtonText => _selectedScenario switch
    {
        QuickStartScenario.FolderWorkspace => "Open folder workspace",
        QuickStartScenario.EncryptedStorage when IsEncryptedCreateNewSelected => "Create encrypted storage",
        QuickStartScenario.EncryptedStorage => "Open encrypted storage",
        QuickStartScenario.SingleFile when IsSingleFileCreateNewSelected => "Create file",
        _ => "Open file"
    };

    public string SummaryScenarioText => _selectedScenario switch
    {
        QuickStartScenario.FolderWorkspace => "Folder workspace",
        QuickStartScenario.EncryptedStorage => "Encrypted storage",
        _ => "Single file"
    };

    public string SummaryModeText => _selectedScenario switch
    {
        QuickStartScenario.FolderWorkspace => "Open existing folder",
        QuickStartScenario.EncryptedStorage when IsEncryptedCreateNewSelected => "Create new encrypted storage",
        QuickStartScenario.EncryptedStorage => "Open existing encrypted storage",
        QuickStartScenario.SingleFile when IsSingleFileCreateNewSelected => "Create new file",
        _ => "Open existing file"
    };

    public string SummaryPathText => _selectedScenario switch
    {
        QuickStartScenario.FolderWorkspace => GetDisplayPath(_folderPath),
        QuickStartScenario.EncryptedStorage => GetDisplayPath(_encryptedPath),
        _ => GetDisplayPath(_singleFilePath)
    };

    public string SummarySecurityText => _selectedScenario switch
    {
        QuickStartScenario.EncryptedStorage when IsEncryptedCreateNewSelected => "Password provided and confirmed for a new archive.",
        QuickStartScenario.EncryptedStorage => "Password provided for opening the archive.",
        _ => "No additional password is required."
    };

    public string SummaryLaunchNote => _selectedScenario switch
    {
        QuickStartScenario.FolderWorkspace => "The runtime can open the selected directory as the workspace root.",
        QuickStartScenario.EncryptedStorage when IsEncryptedCreateNewSelected => "The runtime can create the archive, apply the password, and continue into encrypted storage mode.",
        QuickStartScenario.EncryptedStorage => "The runtime can unlock the archive and continue into encrypted storage mode.",
        QuickStartScenario.SingleFile when IsSingleFileCreateNewSelected => "The runtime can create the file at this path and continue with the document flow.",
        _ => "The runtime can open the file directly without switching to a folder-first setup."
    };

    private bool IsCurrentConfigurationValid => _selectedScenario switch
    {
        QuickStartScenario.FolderWorkspace => !HasFolderPathError,
        QuickStartScenario.EncryptedStorage => !HasEncryptedPathError && !HasEncryptedPasswordError && !HasEncryptedConfirmationError,
        _ => !HasSingleFilePathError
    };

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep <= 1)
        {
            return;
        }

        _currentStep--;
        RefreshState();
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanGoNext)
        {
            return;
        }

        if (_currentStep < 3)
        {
            _currentStep++;
            RefreshState();
        }
    }

    private void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildRequest(out QuickStartRequest request))
        {
            return;
        }

        QuickStartRequested?.Invoke(this, new QuickStartRequestEventArgs(request));
    }

    private void BrowseFolderPathButton_Click(object sender, RoutedEventArgs e)
    {
        using Forms.FolderBrowserDialog dialog = new()
        {
            Description = "Choose workspace folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            InitialDirectory = GetInitialDirectory(_folderPath, treatAsFilePath: false)
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            FolderPath = dialog.SelectedPath;
        }
    }

    private void BrowseEncryptedPathButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsEncryptedOpenExistingSelected)
        {
            Microsoft.Win32.OpenFileDialog dialog = new()
            {
                Title = "Open encrypted storage",
                Filter = "Encrypted storages (*.wstore)|*.wstore|All files (*.*)|*.*",
                CheckFileExists = true,
                InitialDirectory = GetInitialDirectory(_encryptedPath, treatAsFilePath: true)
            };

            if (dialog.ShowDialog() == true)
            {
                EncryptedPath = dialog.FileName;
            }

            return;
        }

        Microsoft.Win32.SaveFileDialog saveDialog = new()
        {
            Title = "Create encrypted storage",
            Filter = "Encrypted storages (*.wstore)|*.wstore",
            DefaultExt = ".wstore",
            AddExtension = true,
            OverwritePrompt = false,
            InitialDirectory = GetInitialDirectory(_encryptedPath, treatAsFilePath: true),
            FileName = GetInitialFileName(_encryptedPath, DefaultEncryptedFileName)
        };

        if (saveDialog.ShowDialog() == true)
        {
            EncryptedPath = saveDialog.FileName;
        }
    }
    private void BrowseSingleFilePathButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsSingleFileOpenExistingSelected)
        {
            Microsoft.Win32.OpenFileDialog dialog = new()
            {
                Title = "Open file",
                Filter = "All files (*.*)|*.*|Text documents (*.txt)|*.txt",
                CheckFileExists = true,
                InitialDirectory = GetInitialDirectory(_singleFilePath, treatAsFilePath: true)
            };

            if (dialog.ShowDialog() == true)
            {
                SingleFilePath = dialog.FileName;
            }

            return;
        }

        Microsoft.Win32.SaveFileDialog saveDialog = new()
        {
            Title = "Create file",
            Filter = "All files (*.*)|*.*|Text documents (*.txt)|*.txt",
            DefaultExt = ".txt",
            AddExtension = true,
            OverwritePrompt = false,
            InitialDirectory = GetInitialDirectory(_singleFilePath, treatAsFilePath: true),
            FileName = GetInitialFileName(_singleFilePath, DefaultDocumentFileName)
        };

        if (saveDialog.ShowDialog() == true)
        {
            SingleFilePath = saveDialog.FileName;
        }
    }

    private void EncryptedPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _encryptedPassword = passwordBox.Password;
            RefreshState();
        }
    }

    private void EncryptedConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _encryptedConfirmation = passwordBox.Password;
            RefreshState();
        }
    }

    private void SetScenario(QuickStartScenario scenario)
    {
        if (_selectedScenario == scenario)
        {
            return;
        }

        _selectedScenario = scenario;
        RefreshState();
    }

    private void SetEncryptedMode(QuickStartMode mode)
    {
        if (_encryptedMode == mode)
        {
            return;
        }

        _encryptedMode = mode;
        RefreshState();
    }

    private void SetSingleFileMode(QuickStartMode mode)
    {
        if (_singleFileMode == mode)
        {
            return;
        }

        _singleFileMode = mode;
        RefreshState();
    }

    private void RefreshState()
    {
        _folderPathError = ValidateFolderPath();
        _encryptedPathError = ValidateEncryptedPath();
        _encryptedPasswordError = ValidateEncryptedPassword();
        _encryptedConfirmationError = ValidateEncryptedConfirmation();
        _singleFilePathError = ValidateSingleFilePath();
        NotifyAllPropertiesChanged();
    }

    private string ValidateFolderPath()
    {
        if (!IsFolderScenarioSelected)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_folderPath))
        {
            return "Choose an existing folder workspace.";
        }

        string? fullPath = TryGetFullPath(_folderPath);
        if (fullPath is null)
        {
            return "Enter a valid folder path.";
        }

        return Directory.Exists(fullPath) ? string.Empty : "The folder must already exist.";
    }

    private string ValidateEncryptedPath()
    {
        if (!IsEncryptedScenarioSelected)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_encryptedPath))
        {
            return IsEncryptedCreateNewSelected
                ? "Choose where the new encrypted storage should be created."
                : "Choose an existing encrypted storage file.";
        }

        string? fullPath = TryGetFullPath(_encryptedPath);
        if (fullPath is null)
        {
            return "Enter a valid storage path.";
        }

        if (IsEncryptedOpenExistingSelected)
        {
            return File.Exists(fullPath) ? string.Empty : "Open existing requires a storage file that already exists.";
        }

        if (!HasFileName(fullPath))
        {
            return "Enter a file name for the new storage.";
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".wstore", StringComparison.OrdinalIgnoreCase))
        {
            return "Use the .wstore extension for a new encrypted storage.";
        }

        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return "Choose an existing folder for the new storage.";
        }

        return File.Exists(fullPath) ? "Create new expects a file path that does not already exist." : string.Empty;
    }

    private string ValidateEncryptedPassword()
    {
        if (!IsEncryptedScenarioSelected)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(_encryptedPassword) ? "Enter the storage password." : string.Empty;
    }

    private string ValidateEncryptedConfirmation()
    {
        if (!IsEncryptedScenarioSelected || !IsEncryptedCreateNewSelected)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_encryptedConfirmation))
        {
            return "Confirm the storage password.";
        }

        return string.Equals(_encryptedPassword, _encryptedConfirmation, StringComparison.Ordinal)
            ? string.Empty
            : "Passwords do not match.";
    }

    private string ValidateSingleFilePath()
    {
        if (!IsSingleFileScenarioSelected)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_singleFilePath))
        {
            return IsSingleFileCreateNewSelected
                ? "Choose where the new file should be created."
                : "Choose an existing file to open.";
        }

        string? fullPath = TryGetFullPath(_singleFilePath);
        if (fullPath is null)
        {
            return "Enter a valid file path.";
        }

        if (IsSingleFileOpenExistingSelected)
        {
            return File.Exists(fullPath) ? string.Empty : "Open existing requires a file that already exists.";
        }

        if (!HasFileName(fullPath))
        {
            return "Enter a file name for the new document.";
        }

        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return "Choose an existing folder for the new file.";
        }

        return File.Exists(fullPath) ? "Create new expects a file path that does not already exist." : string.Empty;
    }

    private bool TryBuildRequest(out QuickStartRequest request)
    {
        request = default!;

        if (!IsCurrentConfigurationValid)
        {
            return false;
        }

        string? targetPath = _selectedScenario switch
        {
            QuickStartScenario.FolderWorkspace => TryGetFullPath(_folderPath),
            QuickStartScenario.EncryptedStorage => TryGetFullPath(_encryptedPath),
            _ => TryGetFullPath(_singleFilePath)
        };

        if (targetPath is null)
        {
            return false;
        }

        request = _selectedScenario switch
        {
            QuickStartScenario.FolderWorkspace => new QuickStartRequest(QuickStartScenario.FolderWorkspace, QuickStartMode.OpenExisting, targetPath, null),
            QuickStartScenario.EncryptedStorage => new QuickStartRequest(QuickStartScenario.EncryptedStorage, _encryptedMode, targetPath, _encryptedPassword),
            _ => new QuickStartRequest(QuickStartScenario.SingleFile, _singleFileMode, targetPath, null)
        };

        return true;
    }

    private static string GetInitialDirectory(string? path, bool treatAsFilePath)
    {
        string fallback = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string? fullPath = TryGetFullPath(path);
        if (fullPath is null)
        {
            return fallback;
        }

        string? preferred = treatAsFilePath ? Path.GetDirectoryName(fullPath) : fullPath;
        if (!string.IsNullOrWhiteSpace(preferred) && Directory.Exists(preferred))
        {
            return preferred;
        }

        string? parent = Path.GetDirectoryName(fullPath);
        return !string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent) ? parent : fallback;
    }

    private static string GetInitialFileName(string? path, string fallback)
    {
        string? fullPath = TryGetFullPath(path);
        if (fullPath is null)
        {
            return fallback;
        }

        string fileName = Path.GetFileName(fullPath);
        return string.IsNullOrWhiteSpace(fileName) ? fallback : fileName;
    }

    private static string GetDisplayPath(string path)
    {
        return TryGetFullPath(path) ?? path.Trim();
    }

    private static bool HasFileName(string fullPath)
    {
        return !string.IsNullOrWhiteSpace(Path.GetFileName(fullPath));
    }

    private static string? TryGetFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch
        {
            return null;
        }
    }

    private static Visibility ToVisibility(bool isVisible)
    {
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void NotifyAllPropertiesChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum QuickStartScenario
{
    FolderWorkspace,
    EncryptedStorage,
    SingleFile
}

public enum QuickStartMode
{
    OpenExisting,
    CreateNew
}

public sealed record QuickStartRequest(QuickStartScenario Scenario, QuickStartMode Mode, string TargetPath, string? Password);

public sealed class QuickStartRequestEventArgs : EventArgs
{
    public QuickStartRequestEventArgs(QuickStartRequest request)
    {
        Request = request;
    }

    public QuickStartRequest Request { get; }
}

