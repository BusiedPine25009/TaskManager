using Avalonia.Controls;
using TaskManager.Services;

namespace TaskManager.Dialogs;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog() : this(Loc.T("dlg.confirm.deleteTask"))
    {
    }

    public ConfirmationDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    private void CancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
    private void ConfirmClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(true);
}
