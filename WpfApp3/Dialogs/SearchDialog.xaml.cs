using System.Windows;
using WpfApp3.Services;

namespace WpfApp3.Dialogs;

public partial class SearchDialog : Window
{
    public SearchDialog(string initialQuery)
    {
        InitializeComponent();
        SearchQuery = initialQuery;
        QueryTextBox.Text = initialQuery;
        Loaded += SearchDialog_Loaded;
        ApplyLocalization();
    }

    public string SearchQuery { get; private set; }

    private void SearchDialog_Loaded(object sender, RoutedEventArgs e)
    {
        QueryTextBox.Focus();
        QueryTextBox.SelectAll();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        SearchQuery = QueryTextBox.Text;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.Instance["SearchDialogTitle"];
        PromptTextBlock.Text = LocalizationService.Instance["SearchDialogPrompt"];
        ApplyButton.Content = LocalizationService.Instance["SearchDialogApply"];
        CancelButton.Content = LocalizationService.Instance["SearchDialogCancel"];
    }
}
