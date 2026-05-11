using System.Windows;
using System.Windows.Input;

namespace WpfApp3.Dialogs;

public partial class PasswordDialog : Window
{
    private readonly bool _requiresConfirmation;
    private readonly string _emptyPasswordMessage;
    private readonly string _mismatchMessage;

    public PasswordDialog(
        string title,
        string prompt,
        string passwordLabel,
        string confirmPasswordLabel,
        string actionText,
        string cancelText,
        string emptyPasswordMessage,
        string mismatchMessage,
        bool requiresConfirmation)
    {
        InitializeComponent();
        Title = title;
        TitleTextBlock.Text = title;
        PromptTextBlock.Text = prompt;
        PasswordLabelTextBlock.Text = passwordLabel;
        ConfirmPasswordLabelTextBlock.Text = confirmPasswordLabel;
        ActionButton.Content = actionText;
        CancelButton.Content = cancelText;
        _emptyPasswordMessage = emptyPasswordMessage;
        _mismatchMessage = mismatchMessage;
        _requiresConfirmation = requiresConfirmation;
        Loaded += PasswordDialog_Loaded;

        if (!_requiresConfirmation)
        {
            ConfirmPasswordLabelTextBlock.Visibility = Visibility.Collapsed;
            ConfirmPasswordValueBox.Visibility = Visibility.Collapsed;
        }
    }

    public string Password => PasswordValueBox.Password;

    private void PasswordDialog_Loaded(object sender, RoutedEventArgs e)
    {
        PasswordValueBox.Focus();
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        Submit();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void HeaderBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void PasswordBoxes_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ValidationTextBlock.Visibility = Visibility.Collapsed;
        ValidationTextBlock.Text = string.Empty;
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Submit();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(PasswordValueBox.Password))
        {
            ShowValidation(_emptyPasswordMessage);
            PasswordValueBox.Focus();
            return;
        }

        if (_requiresConfirmation && !PasswordValueBox.Password.Equals(ConfirmPasswordValueBox.Password, StringComparison.Ordinal))
        {
            ShowValidation(_mismatchMessage);
            ConfirmPasswordValueBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void ShowValidation(string message)
    {
        ValidationTextBlock.Text = message;
        ValidationTextBlock.Visibility = Visibility.Visible;
    }
}