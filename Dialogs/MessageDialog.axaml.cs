using Avalonia.Controls;

namespace TaskManager.Dialogs;

public partial class MessageDialog : Window
{
    public MessageDialog() : this("TaskManager", string.Empty)
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
