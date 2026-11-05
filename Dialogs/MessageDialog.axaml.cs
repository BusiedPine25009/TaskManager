using Avalonia.Controls;
using TaskManager.Services;

namespace TaskManager.Dialogs;

public partial class MessageDialog : Window
{
    public MessageDialog() : this(Loc.T("common.message"), string.Empty)
    {
    }

    public MessageDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
    }

    private void CloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
