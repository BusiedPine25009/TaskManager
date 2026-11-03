using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TaskManager.Dialogs;

public partial class RecoveryDialog : Window
{
    public RecoveryDialog() : this(string.Empty)
    {
    }

    public RecoveryDialog(string message)
    {
        InitializeComponent();
        DataContext = new RecoveryViewModel(message);
    }

    private void CancelClick(object? sender, RoutedEventArgs e) => Close(false);
    private void RestoreClick(object? sender, RoutedEventArgs e) => Close(true);

    private sealed record RecoveryViewModel(string Message);
}
