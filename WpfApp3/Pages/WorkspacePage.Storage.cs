using System.IO;
using System.Text;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using WpfApp3.Dialogs;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.Pages;

public partial class WorkspacePage
{
    private readonly StorageSessionService _storageSessionService = new();
    private StorageReference? _storageReference;
    private StorageSession? _storageSession;

    public event EventHandler<StorageReference?>? StorageReferenceChanged;

    public bool CanUnlockCurrentStorage => _storageReference?.Kind == StorageKind.EncryptedArchive
        && _storageSession is null
        && !string.IsNullOrWhiteSpace(_storageReference.SourcePath);

    public bool OpenStorageFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        try
        {
            string normalizedPath = Path.GetFullPath(folderPath);
            Directory.CreateDirectory(normalizedPath);
            return OpenFolderStorage(normalizedPath);
        }
        catch
        {
            ShowError("MessageOpenStorageFailed");
            return false;
        }
    }

    public void OpenEncryptedStorage()
    {
        EncryptedStorageDebugLog.Write("Workspace", "OpenEncryptedStorage dialog opened");

        Microsoft.Win32.OpenFileDialog dialog = new()
        {
            Filter = "Encrypted storages (*.wstore)|*.wstore|All files (*.*)|*.*",
            DefaultExt = ".wstore",
            CheckFileExists = true,
            Title = Localize("OpenEncryptedStorageDialogTitle")
        };

        if (dialog.ShowDialog() != true)
        {
            EncryptedStorageDebugLog.Write("Workspace", "OpenEncryptedStorage canceled at file dialog");
            return;
        }

        PasswordDialog passwordDialog = CreatePasswordDialog(
            Localize("PasswordDialogUnlockTitle"),
            Localize("PasswordDialogUnlockPrompt"),
            Localize("PasswordDialogUnlockAction"),
            false);

        if (passwordDialog.ShowDialog() != true)
        {
            EncryptedStorageDebugLog.Write("Workspace", $"OpenEncryptedStorage canceled at password dialog; archive={dialog.FileName}");
            return;
        }

        OpenEncryptedStorageInternal(dialog.FileName, passwordDialog.Password);
    }

    public bool OpenEncryptedStorage(string? archivePath, string? password)
    {
        return OpenEncryptedStorageInternal(archivePath, password);
    }

    public void CreateEncryptedStorage()
    {
        EncryptedStorageDebugLog.Write("Workspace", "CreateEncryptedStorage dialog opened");

        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            Filter = "Encrypted storages (*.wstore)|*.wstore",
            DefaultExt = ".wstore",
            AddExtension = true,
            OverwritePrompt = true,
            Title = Localize("CreateEncryptedStorageDialogTitle"),
            FileName = Localize("NewEncryptedStorageFileName")
        };

        if (dialog.ShowDialog() != true)
        {
            EncryptedStorageDebugLog.Write("Workspace", "CreateEncryptedStorage canceled at file dialog");
            return;
        }

        PasswordDialog passwordDialog = CreatePasswordDialog(
            Localize("PasswordDialogCreateTitle"),
            Localize("PasswordDialogCreatePrompt"),
            Localize("PasswordDialogCreateAction"),
            true);

        if (passwordDialog.ShowDialog() != true)
        {
            EncryptedStorageDebugLog.Write("Workspace", $"CreateEncryptedStorage canceled at password dialog; archive={dialog.FileName}");
            return;
        }

        CreateEncryptedStorageInternal(dialog.FileName, passwordDialog.Password);
    }

    public bool CreateEncryptedStorage(string? archivePath, string? password)
    {
        return CreateEncryptedStorageInternal(archivePath, password);
    }

    public void UnlockCurrentStorage()
    {
        if (!CanUnlockCurrentStorage || _storageReference is null)
        {
            return;
        }

        PasswordDialog passwordDialog = CreatePasswordDialog(
            Localize("PasswordDialogUnlockTitle"),
            Localize("PasswordDialogUnlockPrompt"),
            Localize("PasswordDialogUnlockAction"),
            false);

        if (passwordDialog.ShowDialog() != true)
        {
            EncryptedStorageDebugLog.Write("Workspace", $"UnlockCurrentStorage canceled at password dialog; archive={_storageReference.SourcePath}");
            return;
        }

        OpenEncryptedStorageInternal(_storageReference.SourcePath, passwordDialog.Password);
    }
    public bool CloseCurrentStorage()
    {
        EncryptedStorageDebugLog.Write("Workspace", $"CloseCurrentStorage start; current={_storageReference?.SourcePath ?? "<null>"}");

        if (!ResolvePendingChanges())
        {
            EncryptedStorageDebugLog.Write("Workspace", "CloseCurrentStorage canceled by ResolvePendingChanges");
            return false;
        }

        bool keepEncryptedReference = _storageReference?.Kind == StorageKind.EncryptedArchive && _storageSession is not null;

        if (!CloseActiveStorageSession())
        {
            EncryptedStorageDebugLog.Write("Workspace", "CloseCurrentStorage failed while closing active session");
            return false;
        }

        _contextMenuNode = null;
        _selectedTreePath = null;

        if (!keepEncryptedReference)
        {
            _storageReference = null;
        }

        ResetDocumentState();
        LoadStorageTree();
        RaiseStorageReferenceChanged();
        EncryptedStorageDebugLog.Write("Workspace", $"CloseCurrentStorage success; keepEncryptedReference={keepEncryptedReference}");
        return true;
    }

    private void InitializeStorageState(StorageReference? storageReference)
    {
        EncryptedStorageDebugLog.Write("Workspace", $"InitializeStorageState; referenceKind={storageReference?.Kind.ToString() ?? "<null>"}; referencePath={storageReference?.SourcePath ?? "<null>"}");
        _storageSessionService.CleanupStaleUnlockedStorages();
        _storageReference = storageReference;

        if (storageReference?.Kind == StorageKind.Folder && !string.IsNullOrWhiteSpace(storageReference.SourcePath) && Directory.Exists(storageReference.SourcePath))
        {
            _storageSession = _storageSessionService.OpenFolder(storageReference.SourcePath);
            _storageFolderPath = _storageSession.WorkingRootPath;
            return;
        }

        _storageFolderPath = null;
    }

    public bool OpenFolderStorage(string? folderPath)
    {
        string? normalizedFolderPath = NormalizeExistingDirectoryPath(folderPath);

        if (string.IsNullOrWhiteSpace(normalizedFolderPath))
        {
            return false;
        }

        folderPath = normalizedFolderPath;

        if (string.Equals(_storageReference?.SourcePath, folderPath, StringComparison.OrdinalIgnoreCase)
            && _storageReference?.Kind == StorageKind.Folder
            && _storageSession is not null)
        {
            EncryptedStorageDebugLog.Write("Workspace", $"OpenFolderStorage skipped; already open; folder={folderPath}");
            return true;
        }

        StorageReference reference = new(StorageKind.Folder, folderPath);
        return TrySwitchStorage(() => _storageSessionService.OpenFolder(folderPath), _ => reference);
    }

    private bool SwitchToFileDirectoryStorage(string filePath)
    {
        string? folderPath = Path.GetDirectoryName(filePath);
        return !string.IsNullOrWhiteSpace(folderPath) && OpenFolderStorage(folderPath);
    }

    private bool ShouldCommitCurrentStorageChange(string filePath)
    {
        return _storageSession is { IsEncrypted: true } && IsPathInCurrentStorage(filePath);
    }

    private bool IsFileOutsideCurrentStorage(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_storageFolderPath))
        {
            return true;
        }

        return !IsPathInCurrentStorage(filePath);
    }

    private bool IsPathInCurrentStorage(string? path)
    {
        return IsPathWithinDirectory(path, _storageFolderPath);
    }

    private static bool IsPathWithinDirectory(string? path, string? directoryPath)
    {
        string? fullPath = NormalizeFullPath(path);
        string? fullDirectoryPath = NormalizeFullPath(directoryPath);

        if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(fullDirectoryPath))
        {
            return false;
        }

        string prefix = fullDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeExistingDirectoryPath(string? path)
    {
        string? normalizedPath = NormalizeFullPath(path);
        return !string.IsNullOrWhiteSpace(normalizedPath) && Directory.Exists(normalizedPath)
            ? normalizedPath
            : null;
    }

    private static string? NormalizeExistingFilePath(string? path)
    {
        string? normalizedPath = NormalizeFullPath(path);
        return !string.IsNullOrWhiteSpace(normalizedPath) && File.Exists(normalizedPath)
            ? normalizedPath
            : null;
    }

    private string? NormalizeArchivePath(string? archivePath, bool requireExistingFile)
    {
        string? normalizedArchivePath = NormalizeFullPath(archivePath);

        if (string.IsNullOrWhiteSpace(normalizedArchivePath))
        {
            return null;
        }

        normalizedArchivePath = _storageSessionService.EnsureArchiveExtension(normalizedArchivePath);

        if (Directory.Exists(normalizedArchivePath))
        {
            return null;
        }

        return !requireExistingFile || File.Exists(normalizedArchivePath)
            ? normalizedArchivePath
            : null;
    }

    private bool OpenEncryptedStorageInternal(string? archivePath, string? password)
    {
        string? normalizedArchivePath = NormalizeArchivePath(archivePath, requireExistingFile: true);

        if (string.IsNullOrWhiteSpace(normalizedArchivePath) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        archivePath = normalizedArchivePath;
        EncryptedStorageDebugLog.Write("Workspace", $"OpenEncryptedStorageInternal start; archive={archivePath}");

        if (string.Equals(_storageReference?.SourcePath, archivePath, StringComparison.OrdinalIgnoreCase)
            && _storageReference?.Kind == StorageKind.EncryptedArchive
            && _storageSession is not null)
        {
            EncryptedStorageDebugLog.Write("Workspace", $"OpenEncryptedStorageInternal skipped; already unlocked; archive={archivePath}");
            return true;
        }

        return TrySwitchStorage(
            () => _storageSessionService.UnlockEncryptedArchive(archivePath, password),
            session => new StorageReference(StorageKind.EncryptedArchive, session.SourcePath));
    }

    private bool CreateEncryptedStorageInternal(string? archivePath, string? password)
    {
        string? normalizedArchivePath = NormalizeArchivePath(archivePath, requireExistingFile: false);

        if (string.IsNullOrWhiteSpace(normalizedArchivePath)
            || string.IsNullOrWhiteSpace(password)
            || File.Exists(normalizedArchivePath))
        {
            return false;
        }

        string? directory = Path.GetDirectoryName(normalizedArchivePath);

        try
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch
        {
            return false;
        }

        return TrySwitchStorage(
            () => _storageSessionService.CreateEncryptedArchive(normalizedArchivePath, password),
            session => new StorageReference(StorageKind.EncryptedArchive, session.SourcePath));
    }

    private bool TrySwitchStorage(Func<StorageSession> sessionFactory, Func<StorageSession, StorageReference> referenceFactory)
    {
        EncryptedStorageDebugLog.Write("Workspace", "TrySwitchStorage start");

        if (!ResolvePendingChanges())
        {
            EncryptedStorageDebugLog.Write("Workspace", "TrySwitchStorage canceled by ResolvePendingChanges");
            return false;
        }

        StorageSession newSession;

        try
        {
            newSession = sessionFactory();
        }
        catch (StorageOperationException exception)
        {
            EncryptedStorageDebugLog.Write("Workspace", $"TrySwitchStorage storage error; code={exception.ErrorCode}; message={EncryptedStorageDebugLog.Normalize(exception.Message)}");
            HandleStorageOperationError(exception);
            return false;
        }
        catch (Exception exception)
        {
            EncryptedStorageDebugLog.WriteException("Workspace", exception);
            ShowError("MessageOpenStorageFailed");
            return false;
        }

        if (!CloseActiveStorageSession())
        {
            EncryptedStorageDebugLog.Write("Workspace", $"TrySwitchStorage canceled; reason=close_active_storage_failed; pendingKind={newSession.Kind}; pendingSource={newSession.SourcePath}");
            CloseDetachedStorageSession(newSession);
            return false;
        }

        _storageSession = newSession;
        _storageReference = referenceFactory(newSession);
        _storageFolderPath = newSession.WorkingRootPath;
        _contextMenuNode = null;
        _selectedTreePath = null;
        ResetDocumentState();
        LoadStorageTree();
        RaiseStorageReferenceChanged();
        EncryptedStorageDebugLog.Write("Workspace", $"TrySwitchStorage success; kind={newSession.Kind}; source={newSession.SourcePath}; working={newSession.WorkingRootPath}");
        return true;
    }

    private bool CloseActiveStorageSession()
    {
        if (_storageSession is null)
        {
            _storageFolderPath = null;
            return true;
        }

        EncryptedStorageDebugLog.Write("Workspace", $"CloseActiveStorageSession; source={_storageSession.SourcePath}");

        try
        {
            _storageSessionService.Close(_storageSession);
            _storageSession = null;
            _storageFolderPath = null;
            EncryptedStorageDebugLog.Write("Workspace", "CloseActiveStorageSession success");
            return true;
        }
        catch (StorageOperationException exception)
        {
            EncryptedStorageDebugLog.Write("Workspace", $"CloseActiveStorageSession storage error; code={exception.ErrorCode}; message={EncryptedStorageDebugLog.Normalize(exception.Message)}");
            HandleStorageOperationError(exception);
            return false;
        }
        catch (Exception exception)
        {
            EncryptedStorageDebugLog.WriteException("Workspace", exception);
            ShowError("MessageEncryptedOperationFailed", $"Log: {EncryptedStorageDebugLog.LogPath}");
            return false;
        }
    }

    private void CloseDetachedStorageSession(StorageSession session)
    {
        EncryptedStorageDebugLog.Write("Workspace", $"CloseDetachedStorageSession start; kind={session.Kind}; source={session.SourcePath}; working={session.WorkingRootPath}");

        try
        {
            _storageSessionService.Close(session);
        }
        catch (Exception exception)
        {
            EncryptedStorageDebugLog.WriteException("Workspace", exception);
        }
    }

    private bool TryCloseStorageSessionForExit()
    {
        EncryptedStorageDebugLog.Write("Workspace", "TryCloseStorageSessionForExit start");

        if (!ResolvePendingChanges())
        {
            EncryptedStorageDebugLog.Write("Workspace", "TryCloseStorageSessionForExit canceled by ResolvePendingChanges");
            return false;
        }

        if (!CloseActiveStorageSession())
        {
            EncryptedStorageDebugLog.Write("Workspace", "TryCloseStorageSessionForExit failed while closing active session");
            return false;
        }

        EncryptedStorageDebugLog.Write("Workspace", "TryCloseStorageSessionForExit success");
        return true;
    }

    private PasswordDialog CreatePasswordDialog(string title, string prompt, string actionText, bool requiresConfirmation)
    {
        return new PasswordDialog(
            title,
            prompt,
            Localize("PasswordDialogPasswordLabel"),
            Localize("PasswordDialogConfirmPasswordLabel"),
            actionText,
            Localize("DialogCancel"),
            Localize("PasswordDialogEmptyValidation"),
            Localize("PasswordDialogMismatchValidation"),
            requiresConfirmation)
        {
            Owner = System.Windows.Window.GetWindow(this)
        };
    }

    private string GetInitialFolderDirectory()
    {
        if (_storageReference?.Kind == StorageKind.Folder && !string.IsNullOrWhiteSpace(_storageReference.SourcePath))
        {
            return _storageReference.SourcePath;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private string GetStorageEmptyStateText()
    {
        if (_storageReference is null)
        {
            return Localize("StorageUnavailableMessage");
        }

        if (_storageReference.Kind == StorageKind.EncryptedArchive && _storageSession is null)
        {
            return Localize("StorageLockedMessage");
        }

        if (!_storageService.StorageExists(_storageFolderPath))
        {
            return Localize("StorageMissingMessage");
        }

        return Localize("StorageEmptyMessage");
    }

    private bool CommitEncryptedStorageChange(Action rollbackAction, string fallbackErrorKey)
    {
        if (_storageSession is not { IsEncrypted: true })
        {
            return true;
        }

        EncryptedStorageDebugLog.Write("Workspace", $"CommitEncryptedStorageChange start; archive={_storageSession.SourcePath}; working={_storageSession.WorkingRootPath}");

        try
        {
            _storageSession.IsDirty = true;
            _storageSessionService.Pack(_storageSession);
            _storageSession.IsDirty = false;
            EncryptedStorageDebugLog.Write("Workspace", $"CommitEncryptedStorageChange success; archive={_storageSession.SourcePath}");
            return true;
        }
        catch (StorageOperationException exception)
        {
            _storageSession.IsDirty = false;

            try
            {
                rollbackAction();
            }
            catch (Exception rollbackException)
            {
                EncryptedStorageDebugLog.WriteException("Workspace", rollbackException);
            }

            EncryptedStorageDebugLog.Write("Workspace", $"CommitEncryptedStorageChange storage error; code={exception.ErrorCode}; message={EncryptedStorageDebugLog.Normalize(exception.Message)}");
            HandleStorageOperationError(exception, fallbackErrorKey);
            return false;
        }
        catch (Exception exception)
        {
            _storageSession.IsDirty = false;

            try
            {
                rollbackAction();
            }
            catch (Exception rollbackException)
            {
                EncryptedStorageDebugLog.WriteException("Workspace", rollbackException);
            }

            EncryptedStorageDebugLog.WriteException("Workspace", exception);
            ShowError(fallbackErrorKey);
            return false;
        }
    }

    private void HandleStorageOperationError(StorageOperationException exception, string? fallbackKey = null)
    {
        string key = exception.ErrorCode switch
        {
            "invalid_password" => "MessageEncryptedInvalidPassword",
            "archive_corrupted" => "MessageEncryptedArchiveCorrupted",
            "cryptography_missing" => "MessageCryptographyMissing",
            "python_not_found" => "MessagePythonNotFound",
            "cli_missing" => "MessageEncryptedCliMissing",
            "cli_timeout" => "MessageEncryptedOperationTimedOut",
            _ => fallbackKey ?? "MessageEncryptedOperationFailed"
        };

        EncryptedStorageDebugLog.Write("Workspace", $"HandleStorageOperationError; key={key}; code={exception.ErrorCode}; message={EncryptedStorageDebugLog.Normalize(exception.Message)}");
        ShowError(key, BuildEncryptedStorageDebugDetails(exception));
    }

    private static string BuildEncryptedStorageDebugDetails(StorageOperationException exception)
    {
        StringBuilder builder = new();
        builder.Append("Code: ");
        builder.Append(exception.ErrorCode);

        if (!string.IsNullOrWhiteSpace(exception.Message) && !string.Equals(exception.Message, exception.ErrorCode, StringComparison.Ordinal))
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.Append(exception.Message.Trim());
        }

        builder.AppendLine();
        builder.AppendLine();
        builder.Append("Log: ");
        builder.Append(EncryptedStorageDebugLog.LogPath);
        return builder.ToString();
    }

    private static void RestoreFileFromBackup(string targetPath, bool existedBefore, byte[]? previousBytes)
    {
        if (existedBefore && previousBytes is not null)
        {
            File.WriteAllBytes(targetPath, previousBytes);
            return;
        }

        DeleteIfExists(targetPath);
    }

    private static string BuildBackupPath(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        string backupFileName = $"WpfApp3-{Guid.NewGuid():N}{extension}.bak";
        return Path.Combine(Path.GetTempPath(), backupFileName);
    }

    private static void RestoreDeletedFile(string backupPath, string targetPath)
    {
        if (!File.Exists(backupPath))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(targetPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        DeleteIfExists(targetPath);
        File.Copy(backupPath, targetPath, true);
        DeleteIfExists(backupPath);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private void RaiseStorageReferenceChanged()
    {
        StorageReferenceChanged?.Invoke(this, _storageReference);
    }
}
