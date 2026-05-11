using System.IO;

namespace WpfApp3.Models;

public sealed class StorageReference
{
    public StorageReference(StorageKind kind, string sourcePath)
    {
        Kind = kind;
        SourcePath = sourcePath;
    }

    public StorageKind Kind { get; }

    public string SourcePath { get; }

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