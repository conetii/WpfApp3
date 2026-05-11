using System.Windows.Input;

namespace WpfApp3;

public static class AppCommands
{
    public static RoutedUICommand OpenFileCommand { get; } = new(
        "Open file",
        nameof(OpenFileCommand),
        typeof(AppCommands),
        [new KeyGesture(Key.O, ModifierKeys.Control | ModifierKeys.Shift)]);

    public static RoutedUICommand SaveAsCommand { get; } = new(
        "Save as",
        nameof(SaveAsCommand),
        typeof(AppCommands),
        [new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)]);
    public static RoutedUICommand CloseDocumentCommand { get; } = new(
        "Close document",
        nameof(CloseDocumentCommand),
        typeof(AppCommands),
        [new KeyGesture(Key.W, ModifierKeys.Control)]);

    public static RoutedUICommand DeleteDocumentCommand { get; } = new(
        "Delete document",
        nameof(DeleteDocumentCommand),
        typeof(AppCommands),
        [new KeyGesture(Key.Delete)]);
}
