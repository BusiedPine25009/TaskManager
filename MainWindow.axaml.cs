using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using TaskManager.Dialogs;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.ViewModels;

namespace TaskManager;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly TaskService _taskService;
    private readonly JsonTaskStorage _taskStorage;
    private readonly RecycleBinService _recycleBin;
    private readonly ThemePreferenceService _themePreferences = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _toastTimer;
    private string? _loadError;
    private int _totalCount, _doneCount, _overdueCount;
    private int _binCount;
    private bool _isEmptyStateVisible, _isToastVisible;
    private string _emptyTitle = "Пока нет задач";
    private string _emptyMessage = "Добавь первую задачу, чтобы начать планирование";
    private string _toastMessage = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _recycleBin = new RecycleBinService(new RecycleBinStorage());
        _taskStorage = new JsonTaskStorage();
        _taskService = new TaskService(_taskStorage, _recycleBin);
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _refreshTimer.Tick += (_, _) => Refresh();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _toastTimer.Tick += (_, _) => HideToast();

        StateFilter.ItemsSource = new[]
        {
            new FilterOption<TaskState>("Все статусы", null), new FilterOption<TaskState>("Новые", TaskState.New),
            new FilterOption<TaskState>("В работе", TaskState.InProgress), new FilterOption<TaskState>("Выполненные", TaskState.Done)
        };
        PriorityFilter.ItemsSource = new[]
        {
            new FilterOption<Priority>("Все приоритеты", null), new FilterOption<Priority>("Низкий", Priority.Low),
            new FilterOption<Priority>("Средний", Priority.Medium), new FilterOption<Priority>("Высокий", Priority.High)
        };
        StateFilter.SelectedIndex = 0;
        PriorityFilter.SelectedIndex = 0;

        try { _taskService.Load(); }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            _loadError = ex.Message;
        }
        try { _recycleBin.Load(); }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            _loadError = string.IsNullOrEmpty(_loadError)
                ? ex.Message
                : $"{_loadError}\n\n{ex.Message}";
        }

        Loaded += MainWindowLoaded;
        Closed += (_, _) => { _refreshTimer.Stop(); _toastTimer.Stop(); };
        Refresh();
        _refreshTimer.Start();
    }

    public ObservableCollection<TaskCardViewModel> VisibleTasks { get; } = new();
    public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }
    public int DoneCount { get => _doneCount; private set => SetField(ref _doneCount, value); }
    public int OverdueCount { get => _overdueCount; private set => SetField(ref _overdueCount, value); }
    public int BinCount { get => _binCount; private set => SetField(ref _binCount, value); }
    public string BinButtonText => BinCount > 0 ? $"Корзина ({BinCount})" : "Корзина";
    public bool IsEmptyStateVisible { get => _isEmptyStateVisible; private set => SetField(ref _isEmptyStateVisible, value); }
    public string EmptyTitle { get => _emptyTitle; private set => SetField(ref _emptyTitle, value); }
    public string EmptyMessage { get => _emptyMessage; private set => SetField(ref _emptyMessage, value); }
    public bool IsToastVisible { get => _isToastVisible; private set => SetField(ref _isToastVisible, value); }
    public string ToastMessage { get => _toastMessage; private set => SetField(ref _toastMessage, value); }
    public string ThemeButtonText => CurrentThemeName() switch
    {
        "Light" => "☀",
        "Dark" => "☾",
        _ => "◐"
    };
    public string ThemeTooltip => CurrentThemeName() switch
    {
        "Light" => "Светлая тема",
        "Dark" => "Тёмная тема",
        _ => "Системная тема"
    };

    public new event PropertyChangedEventHandler? PropertyChanged;

    private async void MainWindowLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= MainWindowLoaded;
        if (_loadError != null)
        {
            await new MessageDialog("Не удалось загрузить данные", _loadError).ShowDialog(this);
            if (File.Exists(_taskStorage.BackupFilePath))
            {
                bool restore = await new RecoveryDialog(
                    $"Основной файл задач повреждён. Найдена резервная копия:\n{_taskStorage.BackupFilePath}\n\nВосстановить её?").ShowDialog<bool>(this);
                if (restore)
                {
                    try
                    {
                        _taskService.RestoreFromBackup();
                        _loadError = null;
                        Refresh();
                        ShowToast("Данные восстановлены из резервной копии");
                    }
                    catch (Exception ex) { await ShowError(ex.Message); }
                }
            }
        }
    }

    private async void AddTaskClick(object? sender, RoutedEventArgs e)
    {
        TaskItem? task = await new TaskEditWindow().ShowDialog<TaskItem?>(this);
        if (task == null) return;
        try { _taskService.Add(task); Refresh(); ShowToast("Задача успешно добавлена"); }
        catch (Exception ex) { await ShowError(ex.Message); }
    }

    private async void EditTaskClick(object? sender, RoutedEventArgs e)
    {
        if (GetTask(sender) is not { } current) return;
        TaskItem? replacement = await new TaskEditWindow(current).ShowDialog<TaskItem?>(this);
        if (replacement == null) return;
        try { _taskService.Update(replacement); Refresh(); ShowToast("Изменения сохранены"); }
        catch (Exception ex) { await ShowError(ex.Message); }
    }

    private async void DeleteTaskClick(object? sender, RoutedEventArgs e)
    {
        if (GetTask(sender) is not { } task) return;
        if (!await new ConfirmationDialog().ShowDialog<bool>(this)) return;
        try { _taskService.Remove(task.Id); Refresh(); ShowToast("Задача перемещена в корзину"); }
        catch (Exception ex) { await ShowError(ex.Message); }
    }

    private async void MarkDoneClick(object? sender, RoutedEventArgs e)
    {
        if (GetTask(sender) is not { } task) return;
        try { _taskService.MarkDone(task.Id); Refresh(); ShowToast(task is RecurringTask ? "Срок повторяющейся задачи перенесён" : "Задача выполнена"); }
        catch (Exception ex) { await ShowError(ex.Message); }
    }

    private async void OpenBinClick(object? sender, RoutedEventArgs e)
    {
        RecycleBinResult? result = await new RecycleBinWindow(_recycleBin).ShowDialog<RecycleBinResult?>(this);
        if (result is { Action: RecycleBinAction.Restore })
        {
            try
            {
                TaskItem task = _recycleBin.Get(result.TaskId);
                _taskService.Restore(task);
                _recycleBin.RemoveRestored(result.TaskId);
                Refresh();
                ShowToast("Задача восстановлена");
            }
            catch (Exception ex) { await ShowError(ex.Message); }
        }
        else if (result is { Action: RecycleBinAction.DeletePermanently })
        {
            Refresh();
            ShowToast("Задача окончательно удалена");
        }
        else if (result is { Action: RecycleBinAction.Clear })
        {
            Refresh();
            ShowToast("Корзина очищена");
        }
        else
        {
            Refresh();
        }
    }

    private void ThemeClick(object? sender, RoutedEventArgs e)
    {
        ThemeVariant next = CurrentThemeName() switch
        {
            "Default" => ThemeVariant.Light,
            "Light" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = next;
        _themePreferences.Save(next == ThemeVariant.Light ? "Light" : next == ThemeVariant.Dark ? "Dark" : "Default");
        Raise(nameof(ThemeButtonText));
        Raise(nameof(ThemeTooltip));
    }

    private void FilterChanged(object? sender, RoutedEventArgs e) => Refresh();
    private void ResetFiltersClick(object? sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty; StateFilter.SelectedIndex = 0; PriorityFilter.SelectedIndex = 0; Refresh();
    }

    private void Refresh()
    {
        TotalCount = _taskService.TotalCount;
        DoneCount = _taskService.DoneCount;
        OverdueCount = _taskService.OverdueCount;
        BinCount = _recycleBin.Count;
        Raise(nameof(BinButtonText));
        TaskState? state = (StateFilter.SelectedItem as FilterOption<TaskState>)?.Value;
        Priority? priority = (PriorityFilter.SelectedItem as FilterOption<Priority>)?.Value;
        var tasks = _taskService.Find(SearchBox.Text, state, priority);
        VisibleTasks.Clear();
        foreach (TaskItem task in tasks) VisibleTasks.Add(new TaskCardViewModel(task));
        IsEmptyStateVisible = VisibleTasks.Count == 0;
        bool hasFilters = !string.IsNullOrWhiteSpace(SearchBox.Text) || state.HasValue || priority.HasValue;
        EmptyTitle = hasFilters ? "Ничего не найдено" : "Пока нет задач";
        EmptyMessage = hasFilters ? "Измени поисковый запрос или сбрось фильтры" : "Добавь первую задачу, чтобы начать планирование";
    }

    private void ShowToast(string message)
    {
        ToastMessage = message;
        IsToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void HideToast() { _toastTimer.Stop(); IsToastVisible = false; }
    private void CloseToastClick(object? sender, RoutedEventArgs e) => HideToast();
    private async System.Threading.Tasks.Task ShowError(string message) => await new MessageDialog("Ошибка", message).ShowDialog(this);
    private static TaskItem? GetTask(object? sender) => (sender as Button)?.Tag is TaskCardViewModel card ? card.Task : null;
    private void Raise(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    private static string CurrentThemeName() => Application.Current?.RequestedThemeVariant?.ToString() ?? "Default";
    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value; Raise(name!);
    }

    private sealed record FilterOption<T>(string Label, T? Value) where T : struct
    {
        public override string ToString() => Label;
    }
}
