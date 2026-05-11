using System.Windows;
using System.Windows.Input;

namespace WpfApp3.Dialogs;

public partial class TextInputDialog : Window
{
    public TextInputDialog(string title, string prompt, string actionText, string cancelText, string initialValue)
    {
        InitializeComponent();
        Title = title;
        TitleTextBlock.Text = title;
        PromptTextBlock.Text = prompt;
        ActionButton.Content = actionText;
        CancelButton.Content = cancelText;
        ValueTextBox.Text = initialValue;
        Loaded += TextInputDialog_Loaded;
    }

    public string InputText => ValueTextBox.Text.Trim();

    private void TextInputDialog_Loaded(object sender, RoutedEventArgs e)
    {
        ValueTextBox.Focus();
        ValueTextBox.SelectAll();
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
        if (string.IsNullOrWhiteSpace(InputText))
        {
            ValueTextBox.Focus();
            return;
        }

        DialogResult = true;
    }
}

