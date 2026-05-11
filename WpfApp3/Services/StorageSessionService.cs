using System.IO;
using WpfApp3.Models;

namespace WpfApp3.Services;

public sealed class StorageSessionService
{
    private readonly EncryptedStorageCliService _encryptedStorageCliService = new();
    private readonly string _unlockedStoragesRootPath;

    public StorageSessionService()
    {
        _unlockedStoragesRootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WpfApp3", "UnlockedStorages");
        EncryptedStorageDebugLog.Write("Session", $"Service created; unlockedRoot={_unlockedStoragesRootPath}");
    }

    public StorageSession OpenFolder(string folderPath)
    {
        EncryptedStorageDebugLog.Write("Session", $"OpenFolder; path={folderPath}");
        return new StorageSession(StorageKind.Folder, folderPath, folderPath, true, null);
    }

    public StorageSession CreateEncryptedArchive(string archivePath, string password)
    {
        string normalizedPath = EnsureArchiveExtension(archivePath);
        EncryptedStorageDebugLog.Write("Session", $"CreateEncryptedArchive start; source={archivePath}; normalized={normalizedPath}");
        _encryptedStorageCliService.CreateArchive(normalizedPath, password);
        string workingPath = CreateWorkingDirectory();
        EncryptedStorageDebugLog.Write("Session", $"CreateEncryptedArchive success; archive={normalizedPath}; working={workingPath}");
        return new StorageSession(StorageKind.EncryptedArchive, normalizedPath, workingPath, true, password);
    }

    public StorageSession UnlockEncryptedArchive(string archivePath, string password)
    {
        string workingPath = CreateWorkingDirectory();
        EncryptedStorageDebugLog.Write("Session", $"UnlockEncryptedArchive start; archive={archivePath}; working={workingPath}");

        try
        {
            _encryptedStorageCliService.UnlockArchive(archivePath, workingPath, password);
            EncryptedStorageDebugLog.Write("Session", $"UnlockEncryptedArchive success; archive={archivePath}; working={workingPath}");
            return new StorageSession(StorageKind.EncryptedArchive, archivePath, workingPath, true, password);
        }
        catch (Exception exception)
        {
            EncryptedStorageDebugLog.WriteException("Session", exception);
            DeleteDirectory(workingPath);
            throw;
        }
    }

    public void Pack(StorageSession session)
    {
        if (!session.IsEncrypted)
        {
            EncryptedStorageDebugLog.Write("Session", $"Pack skipped; storageKind={session.Kind}");
            return;
        }

        if (string.IsNullOrWhiteSpace(session.SessionPassword))
        {
            EncryptedStorageDebugLog.Write("Session", $"Pack failed; archive={session.SourcePath}; reason=password_missing");
            throw new StorageOperationException("password_missing");
        }

        EncryptedStorageDebugLog.Write("Session", $"Pack start; archive={session.SourcePath}; working={session.WorkingRootPath}");
        _encryptedStorageCliService.PackArchive(session.SourcePath, session.WorkingRootPath, session.SessionPassword);
        EncryptedStorageDebugLog.Write("Session", $"Pack success; archive={session.SourcePath}");
    }

    public void Close(StorageSession? session)
    {
        if (session is null)
        {
            EncryptedStorageDebugLog.Write("Session", "Close skipped; session=<null>");
            return;
        }

        EncryptedStorageDebugLog.Write("Session", $"Close start; kind={session.Kind}; source={session.SourcePath}; working={session.WorkingRootPath}; unlocked={session.IsUnlocked}");

        if (session.IsEncrypted)
        {
            DeleteDirectory(session.WorkingRootPath);

            if (Directory.Exists(session.WorkingRootPath))
            {
                EncryptedStorageDebugLog.Write("Session", $"Close failed; archive={session.SourcePath}; working={session.WorkingRootPath}; reason=cleanup_failed");
                throw new StorageOperationException(
                    "unlocked_storage_cleanup_failed",
                    $"Failed to delete decrypted temporary storage folder '{session.WorkingRootPath}' for archive '{session.SourcePath}'. The storage remains unlocked so the session state is preserved.");
            }

            session.SessionPassword = null;
            session.IsUnlocked = false;
        }

        EncryptedStorageDebugLog.Write("Session", $"Close finished; kind={session.Kind}; source={session.SourcePath}");
    }

    public void CleanupStaleUnlockedStorages()
    {
        if (!Directory.Exists(_unlockedStoragesRootPath))
        {
            EncryptedStorageDebugLog.Write("Session", "CleanupStaleUnlockedStorages skipped; directory missing");
            return;
        }

        foreach (string directoryPath in Directory.GetDirectories(_unlockedStoragesRootPath))
        {
            EncryptedStorageDebugLog.Write("Session", $"Cleanup stale directory; path={directoryPath}");
            DeleteDirectory(directoryPath);
        }
    }

    public bool IsEncryptedArchivePath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
               && Path.GetExtension(path).Equals(".wstore", StringComparison.OrdinalIgnoreCase);
    }

    public string EnsureArchiveExtension(string archivePath)
    {
        return Path.GetExtension(archivePath).Equals(".wstore", StringComparison.OrdinalIgnoreCase)
            ? archivePath
            : archivePath + ".wstore";
    }

    private string CreateWorkingDirectory()
    {
        Directory.CreateDirectory(_unlockedStoragesRootPath);
        string directoryPath = Path.Combine(_unlockedStoragesRootPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        EncryptedStorageDebugLog.Write("Session", $"CreateWorkingDirectory; path={directoryPath}");
        return directoryPath;
    }

    private static void DeleteDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            EncryptedStorageDebugLog.Write("Session", $"DeleteDirectory skipped; path={directoryPath}");
            return;
        }

        try
        {
            Directory.Delete(directoryPath, true);
            EncryptedStorageDebugLog.Write("Session", $"DeleteDirectory success; path={directoryPath}");
        }
        catch (Exception exception)
        {
            EncryptedStorageDebugLog.Write("Session", $"DeleteDirectory failed; path={directoryPath}; message={EncryptedStorageDebugLog.Normalize(exception.Message)}");
        }
    }
}