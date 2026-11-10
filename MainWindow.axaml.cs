using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
    private readonly AttachmentService _attachments = new();
    private readonly ReminderService _reminders = new();
    private readonly ThemePreferenceService _themePreferences = new();
    private readonly LanguagePreferenceService _languagePreferences = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _toastTimer;
    private readonly DispatcherTimer _reminderTimer;
    private string? _loadError;
    private int _totalCount, _doneCount, _overdueCount;
    private int _binCount;
    private bool _isEmptyStateVisible, _isToastVisible;
    private bool _isRebuildingFilters;
    private string _emptyTitle = string.Empty;
    private string _emptyMessage = string.Empty;
    private string _toastMessage = string.Empty;
    private string? _toastKey;
    private object[] _toastArguments = Array.Empty<object>();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _recycleBin = new RecycleBinService(new RecycleBinStorage(), _attachments);
        _taskStorage = new JsonTaskStorage();
        _taskService = new TaskService(_taskStorage, _recycleBin);
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _refreshTimer.Tick += (_, _) => Refresh();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _toastTimer.Tick += (_, _) => HideToast();
        _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _reminderTimer.Tick += (_, _) => CheckReminders();

        UpdateFilterOptions(null, null);

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
        Loc.Instance.LanguageChanged += LanguageChanged;
        Closed += (_, _) =>
        {
            _refreshTimer.Stop();
            _toastTimer.Stop();
            _reminderTimer.Stop();
            Loc.Instance.LanguageChanged -= LanguageChanged;
        };
        Refresh();
        _refreshTimer.Start();
        _reminderTimer.Start();
        CheckReminders();
    }

    public ObservableCollection<TaskCardViewModel> VisibleTasks { get; } = new();
    public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }
    public int DoneCount { get => _doneCount; private set => SetField(ref _doneCount, value); }
    public int OverdueCount { get => _overdueCount; private set => SetField(ref _overdueCount, value); }
    public int BinCount { get => _binCount; private set => SetField(ref _binCount, value); }
    public string BinButtonText => BinCount > 0 ? Loc.T("main.binWithCount", BinCount) : Loc.T("main.bin");
    public string LanguageButtonText => Loc.T($"lang.name.{Loc.Instance.Language}");
    public bool IsEmptyStateVisible { get => _isEmptyStateVisible; private set => SetField(ref _isEmptyStateVisible, value); }
    public string EmptyTitle { get => _emptyTitle; private set => SetField(ref _emptyTitle, value); }
    public string EmptyMessage { get => _emptyMessage; private set => SetField(ref _emptyMessage, value); }
    public bool IsToastVisible { get => _isToastVisible; private set => SetField(ref _isToastVisible, value); }
    public string ToastMessage { get => _toastMessage; private set => SetField(ref _toastMessage, value); }
    // Какая тема выбрана сейчас: переключатель идёт по кругу «системная → светлая → тёмная»
    public bool IsSystemTheme => CurrentThemeName() == "Default";
    public bool IsLightTheme => CurrentThemeName() == "Light";
    public bool IsDarkTheme => CurrentThemeName() == "Dark";
    public string ThemeLabel => CurrentThemeName() switch
    {
        "Light" => Loc.T("theme.light"),
        "Dark" => Loc.T("theme.dark"),
        _ => Loc.T("theme.auto")
    };
    public string ThemeTooltip => CurrentThemeName() switch
    {
        "Light" => Loc.T("theme.tip.light"),
        "Dark" => Loc.T("theme.tip.dark"),
        _ => Loc.T("theme.tip.system")
    };

    public new event PropertyChangedEventHandler? PropertyChanged;

    private async void MainWindowLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= MainWindowLoaded;
        if (_loadError != null)
        {
            await new MessageDialog(Loc.T("dlg.loadError.title"), _loadError).ShowDialog(this);
            if (File.Exists(_taskStorage.BackupFilePath))
            {
                bool restore = await new RecoveryDialog(
                    Loc.T("dlg.recovery.message", _taskStorage.BackupFilePath)).ShowDialog<bool>(this);
                if (restore)
                {
                    try
                    {
                        _taskService.RestoreFromBackup();
                        _loadError = null;
                        Refresh();
                        ShowToast("toast.restoredBackup");
                    }
                    catch (Exception ex) { await ShowError(ex.Message); }
                }
            }
        }
    }

    private async void AddTaskClick(object? sender, RoutedEventArgs e)
    {
        TaskItem? task = await new TaskEditWindow(null, null, _attachments).ShowDialog<TaskItem?>(this);
        if (task == null) return;
        try { _taskService.Add(task); Refresh(); ShowToast("toast.added"); }
        catch (Exception ex) { await ShowError(ex.Message); }
    }

    private async void EditTaskClick(object? sender, RoutedEventArgs e)
    {
        if (GetTask(sender) is not { } current) return;
        TaskItem? replacement = await new TaskEditWindow(current, null, _attachments).ShowDialog<TaskItem?>(this);
        if (replacement == null) return;
        try
        {
            _taskService.Update(replacement);
            DeleteRemovedAttachments(current, replacement);
            Refresh();
            ShowToast("toast.saved");
        }
        catch (Exception ex) { await ShowError(ex.Message); }
    }

    private async void DeleteTaskClick(object? sender, RoutedEventArgs e)
    {
        if (GetTask(sender) is not { } task) return;
        if (!await new ConfirmationDialog(Loc.T("dlg.confirm.deleteTask")).ShowDialog<bool>(this)) return;
        try { _taskService.Remove(task.Id); Refresh(); ShowToast("toast.movedToBin"); }
        catch (Exception ex) { await ShowError(ex.Message); }
    }

    private async void MarkDoneClick(object? sender, RoutedEventArgs e)
    {
        if (GetTask(sender) is not { } task) return;
        try { _taskService.MarkDone(task.Id); Refresh(); ShowToast(task is RecurringTask ? "toast.recurringShifted" : "toast.done"); }
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
                ShowToast("toast.restored");
            }
            catch (Exception ex) { await ShowError(ex.Message); }
        }
        else if (result is { Action: RecycleBinAction.DeletePermanently })
        {
            Refresh();
            ShowToast("toast.deletedForever");
        }
        else if (result is { Action: RecycleBinAction.Clear })
        {
            Refresh();
            ShowToast("toast.binEmptied");
        }
        else
        {
            Refresh();
        }
    }

    /// <summary>Открывает календарь, использующий текущий список активных задач.</summary>
    private async void OpenCalendarClick(object? sender, RoutedEventArgs e)
    {
        await new CalendarWindow(_taskService, _attachments).ShowDialog(this);
        Refresh();
    }

    /// <summary>Открывает аналитический срез без задач, находящихся в корзине.</summary>
    private async void OpenAnalyticsClick(object? sender, RoutedEventArgs e)
    {
        await new AnalyticsWindow(_taskService).ShowDialog(this);
        Refresh();
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
        Raise(nameof(IsSystemTheme));
        Raise(nameof(IsLightTheme));
        Raise(nameof(IsDarkTheme));
        Raise(nameof(ThemeLabel));
        Raise(nameof(ThemeTooltip));
    }

    /// <summary>Выбирает русский язык, сохраняет его и обновляет открытый интерфейс.</summary>
    private void LanguageRussianClick(object? sender, RoutedEventArgs e) => SelectLanguage("ru");

    /// <summary>Выбирает английский язык, сохраняет его и обновляет открытый интерфейс.</summary>
    private void LanguageEnglishClick(object? sender, RoutedEventArgs e) => SelectLanguage("en");

    /// <summary>Выбирает казахский язык, сохраняет его и обновляет открытый интерфейс.</summary>
    private void LanguageKazakhClick(object? sender, RoutedEventArgs e) => SelectLanguage("kk");

    /// <summary>
    /// Сохраняет выбранный код языка отдельно от настройки темы и применяет его
    /// через singleton локализации; уведомление сервиса обновит привязки и фильтры.
    /// </summary>
    /// <param name="language">Поддерживаемый код ru, en или kk.</param>
    private void SelectLanguage(string language)
    {
        _languagePreferences.Save(language);
        Loc.Instance.SetLanguage(language);
    }

    /// <summary>
    /// Перестраивает подписи фильтров на активном языке, сохраняя выбранные enum-значения.
    /// Блокировка обработчика предотвращает промежуточное обновление списка при перестройке.
    /// </summary>
    /// <param name="selectedState">Статус фильтра до перестройки.</param>
    /// <param name="selectedPriority">Приоритет фильтра до перестройки.</param>
    private void UpdateFilterOptions(TaskState? selectedState, Priority? selectedPriority)
    {
        _isRebuildingFilters = true;
        try
        {
            FilterOption<TaskState>[] states =
            [
                new(Loc.T("filter.allStatuses"), null),
                new(Loc.T("filter.statusNew"), TaskState.New),
                new(Loc.T("filter.statusInProgress"), TaskState.InProgress),
                new(Loc.T("filter.statusDone"), TaskState.Done)
            ];
            FilterOption<Priority>[] priorities =
            [
                new(Loc.T("filter.allPriorities"), null),
                new(Loc.T("enum.priority.low"), Priority.Low),
                new(Loc.T("enum.priority.medium"), Priority.Medium),
                new(Loc.T("enum.priority.high"), Priority.High)
            ];

            StateFilter.ItemsSource = states;
            PriorityFilter.ItemsSource = priorities;
            StateFilter.SelectedItem = states.First(option => option.Value == selectedState);
            PriorityFilter.SelectedItem = priorities.First(option => option.Value == selectedPriority);
        }
        finally
        {
            _isRebuildingFilters = false;
        }
    }

    /// <summary>
    /// Обновляет вычисляемые подписи и карточки после смены языка, не сбрасывая
    /// фильтры, поисковый запрос, тему приложения или уже показанное уведомление.
    /// </summary>
    private void LanguageChanged(object? sender, EventArgs e)
    {
        TaskState? selectedState = (StateFilter.SelectedItem as FilterOption<TaskState>)?.Value;
        Priority? selectedPriority = (PriorityFilter.SelectedItem as FilterOption<Priority>)?.Value;
        UpdateFilterOptions(selectedState, selectedPriority);
        Raise(nameof(LanguageButtonText));
        Raise(nameof(BinButtonText));
        Raise(nameof(ThemeLabel));
        Raise(nameof(ThemeTooltip));
        Refresh();

        if (IsToastVisible && _toastKey != null)
            ToastMessage = Loc.T(_toastKey, _toastArguments);
    }

    private void FilterChanged(object? sender, RoutedEventArgs e)
    {
        if (!_isRebuildingFilters)
            Refresh();
    }

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
        foreach (TaskCardViewModel card in VisibleTasks)
            card.Dispose();
        VisibleTasks.Clear();
        foreach (TaskItem task in tasks) VisibleTasks.Add(new TaskCardViewModel(task));
        IsEmptyStateVisible = VisibleTasks.Count == 0;
        bool hasFilters = !string.IsNullOrWhiteSpace(SearchBox.Text) || state.HasValue || priority.HasValue;
        EmptyTitle = hasFilters ? Loc.T("empty.noResults.title") : Loc.T("empty.noTasks.title");
        EmptyMessage = hasFilters ? Loc.T("empty.noResults.message") : Loc.T("empty.noTasks.message");
    }

    /// <summary>Проверяет наступившие напоминания и показывает локальное уведомление в приложении.</summary>
    private void CheckReminders()
    {
        foreach (TaskItem task in _reminders.CheckDue(_taskService))
            ShowToast("reminder.toast", task.Title);
    }

    /// <summary>Показывает уведомление по ключу локализации на активном языке.</summary>
    /// <param name="key">Ключ уведомления.</param>
    /// <param name="args">Аргументы форматируемой строки, если они нужны.</param>
    private void ShowToast(string key, params object[] args)
    {
        _toastKey = key;
        _toastArguments = args;
        ToastMessage = Loc.T(key, args);
        IsToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void HideToast() { _toastTimer.Stop(); IsToastVisible = false; }
    private void CloseToastClick(object? sender, RoutedEventArgs e) => HideToast();
    private async System.Threading.Tasks.Task ShowError(string message)
        => await new MessageDialog(Loc.T("common.error"), message).ShowDialog(this);
    private static TaskItem? GetTask(object? sender) => (sender as Button)?.Tag is TaskCardViewModel card ? card.Task : null;
    private void DeleteRemovedAttachments(TaskItem previous, TaskItem replacement)
    {
        foreach (TaskAttachment attachment in previous.Attachments)
            if (!replacement.Attachments.Any(current => current.Id == attachment.Id))
                _attachments.Delete(attachment);
    }
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
