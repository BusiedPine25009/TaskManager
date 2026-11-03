using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TaskManager.Services;
using TaskManager.ViewModels;

namespace TaskManager.Dialogs;

public partial class RecycleBinWindow : Window, INotifyPropertyChanged
{
    private readonly RecycleBinService _service;
    private bool _isEmpty;

    public RecycleBinWindow() : this(new RecycleBinService(new RecycleBinStorage()))
    {
    }

    public RecycleBinWindow(RecycleBinService service)
    {
        InitializeComponent();
        _service = service;
        DataContext = this;
        Refresh();
    }

    public ObservableCollection<DeletedTaskViewModel> Entries { get; } = new();
    public bool IsEmpty { get => _isEmpty; private set => SetField(ref _isEmpty, value); }
    public bool HasEntries => !IsEmpty;
    public string CountText => $"Удалённых задач: {Entries.Count}";
    public new event PropertyChangedEventHandler? PropertyChanged;

    private void Refresh()
    {
        Entries.Clear();
        foreach (DeletedTask entry in _service.Entries)
            Entries.Add(new DeletedTaskViewModel(entry));
        IsEmpty = Entries.Count == 0;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasEntries)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CountText)));
    }

    private void RestoreClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not DeletedTaskViewModel item)
            return;
        Close(new RecycleBinResult(RecycleBinAction.Restore, item.Id));
    }

    private async void DeleteClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not DeletedTaskViewModel item)
            return;
        bool confirmed = await new ConfirmationDialog("Вы действительно хотите удалить эту задачу навсегда?").ShowDialog<bool>(this);
        if (!confirmed)
            return;
        _service.DeletePermanently(item.Id);
        Close(new RecycleBinResult(RecycleBinAction.DeletePermanently, item.Id));
    }

    private async void ClearClick(object? sender, RoutedEventArgs e)
    {
        bool confirmed = await new ConfirmationDialog("Вы действительно хотите очистить корзину?").ShowDialog<bool>(this);
        if (!confirmed)
            return;
        _service.Clear();
        Close(new RecycleBinResult(RecycleBinAction.Clear, System.Guid.Empty));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public enum RecycleBinAction
{
    Restore,
    DeletePermanently,
    Clear
}

public sealed record RecycleBinResult(RecycleBinAction Action, System.Guid TaskId);
