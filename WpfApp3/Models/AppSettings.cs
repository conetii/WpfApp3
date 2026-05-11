namespace WpfApp3.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "ru";

    public string Theme { get; set; } = "dark";

    public string? StorageFolderPath { get; set; }

    public StorageKind LastStorageKind { get; set; } = StorageKind.Folder;

    public string? LastStoragePath { get; set; }
}