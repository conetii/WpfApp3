using System.IO;

namespace WpfApp3.Models;

public sealed class StorageSession
{
    public StorageSession(StorageKind kind, string sourcePath, string workingRootPath, bool isUnlocked, string? sessionPassword)
    {
        Kind = kind;
        SourcePath = sourcePath;
        WorkingRootPath = workingRootPath;
        IsUnlocked = isUnlocked;
        SessionPassword = sessionPassword;
    }

    public StorageKind Kind { get; }

    public string SourcePath { get; }

    public string WorkingRootPath { get; }

    public bool IsUnlocked { get; set; }

    public bool IsDirty { get; set; }

    public string? SessionPassword { get; set; }

    public bool IsEncrypted => Kind == StorageKind.EncryptedArchive;

    public string DisplayName
    {
        get
        {
            string trimmed = SourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? SourcePath : name;
        }
    }
}