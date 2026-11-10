using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.ViewModels;

namespace TaskManager.Dialogs;

/// <summary>Месячный календарь активных задач с быстрым открытием обычного редактора.</summary>
public partial class CalendarWindow : Window, INotifyPropertyChanged
{
    private readonly TaskService _tasks;
    private readonly AttachmentService _attachments;
    private DateTime _month;
    private DateTime _selectedDate;
    private bool _isSelectedEmpty;

    public CalendarWindow() : this(
        new TaskService(new JsonTaskStorage()),
        new AttachmentService())
    {
    }

    /// <summary>Создаёт календарь, открытый на текущем месяце и сегодняшней дате.</summary>
    public CalendarWindow(TaskService tasks, AttachmentService attachments)
    {
        InitializeComponent();
        _tasks = tasks;
        _attachments = attachments;
        _month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _selectedDate = DateTime.Today;
        DataContext = this;
        Loc.Instance.LanguageChanged += LanguageChanged;
        Closed += (_, _) => Loc.Instance.LanguageChanged -= LanguageChanged;
        Rebuild();
    }

    /// <summary>Сорок две ячейки видимого месяца, включая соседние дни.</summary>
    public ObservableCollection<CalendarDayViewModel> Days { get; } = new();
    /// <summary>Задачи даты, которую пользователь выбрал в сетке.</summary>
    public ObservableCollection<CalendarTaskViewModel> SelectedTasks { get; } = new();
    /// <summary>Локализованное имя показываемого месяца.</summary>
    public string MonthTitle => _month.ToString("MMMM yyyy");
    /// <summary>Подпись даты выбранного дня.</summary>
    public string SelectedDateTitle => _selectedDate.ToString("dddd, d MMMM");
    /// <summary>Управляет пустым состоянием правой колонки.</summary>
    public bool IsSelectedEmpty { get => _isSelectedEmpty; private set { _isSelectedEmpty = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelectedEmpty))); } }
    public new event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Переходит на предыдущий календарный месяц.</summary>
    private void PreviousMonthClick(object? sender, RoutedEventArgs e) { _month = _month.AddMonths(-1); Rebuild(); }
    /// <summary>Переходит на следующий календарный месяц.</summary>
    private void NextMonthClick(object? sender, RoutedEventArgs e) { _month = _month.AddMonths(1); Rebuild(); }

    /// <summary>Выбирает ячейку месяца и показывает задачи этой даты.</summary>
    private void DayClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not CalendarDayViewModel day) return;
        _selectedDate = day.Date;
        if (day.Date.Month != _month.Month || day.Date.Year != _month.Year)
            _month = new DateTime(day.Date.Year, day.Date.Month, 1);
        Rebuild();
    }

    /// <summary>Открывает стандартный редактор с уже выбранной календарной датой.</summary>
    private async void AddForDayClick(object? sender, RoutedEventArgs e)
    {
        TaskItem? task = await new TaskEditWindow(null, _selectedDate.Date.AddHours(23).AddMinutes(59), _attachments).ShowDialog<TaskItem?>(this);
        if (task == null) return;
        _tasks.Add(task);
        Rebuild();
    }

    /// <summary>Открывает существующий редактор для задачи выбранного дня.</summary>
    private async void EditTaskClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not CalendarTaskViewModel item) return;
        TaskItem? replacement = await new TaskEditWindow(item.Task, null, _attachments).ShowDialog<TaskItem?>(this);
        if (replacement == null) return;
        _tasks.Update(replacement);
        foreach (TaskAttachment attachment in item.Task.Attachments)
            if (!replacement.Attachments.Any(current => current.Id == attachment.Id))
                _attachments.Delete(attachment);
        Rebuild();
    }

    /// <summary>Обновляет названия месяца, дней недели и системные статусы при смене языка.</summary>
    private void LanguageChanged(object? sender, EventArgs e) => Rebuild();

    /// <summary>Перестраивает сетку месяца и список выбранного дня из актуальных задач.</summary>
    private void Rebuild()
    {
        Days.Clear();
        DateTime start = _month.AddDays(-((int)(_month.DayOfWeek + 6) % 7));
        var allTasks = _tasks.GetAll();
        for (int offset = 0; offset < 42; offset++)
            Days.Add(new CalendarDayViewModel(start.AddDays(offset), _month, allTasks, _selectedDate));
        SelectedTasks.Clear();
        foreach (TaskItem task in allTasks)
            if (task.DueDate.Date == _selectedDate.Date)
                SelectedTasks.Add(new CalendarTaskViewModel(task));
        IsSelectedEmpty = SelectedTasks.Count == 0;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MonthTitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDateTitle)));
    }
}
