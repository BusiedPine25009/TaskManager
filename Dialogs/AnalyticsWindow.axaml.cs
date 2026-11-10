using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.ViewModels;

namespace TaskManager.Dialogs;

/// <summary>Окно аналитики активных задач без данных из корзины.</summary>
public partial class AnalyticsWindow : Window, INotifyPropertyChanged
{
    private readonly TaskService _tasks;
    private readonly AnalyticsService _analytics = new();
    private AnalyticsService.Period _period = AnalyticsService.Period.SevenDays;
    private bool _isCompletionEmpty;

    public AnalyticsWindow() : this(new TaskService(new JsonTaskStorage()))
    {
    }

    /// <summary>Создаёт окно и строит срез последних семи дней.</summary>
    public AnalyticsWindow(TaskService tasks)
    {
        InitializeComponent();
        _tasks = tasks;
        DataContext = this;
        Loc.Instance.LanguageChanged += LanguageChanged;
        Closed += (_, _) => Loc.Instance.LanguageChanged -= LanguageChanged;
        Rebuild();
    }

    /// <summary>Набор ключевых показателей выбранного периода.</summary>
    public ObservableCollection<StatisticViewModel> Statistics { get; } = new();
    /// <summary>Данные столбчатого графика.</summary>
    public ObservableCollection<CompletionBarViewModel> CompletionBars { get; } = new();
    /// <summary>Сектора распределения статусов.</summary>
    public ObservableCollection<DonutSegmentViewModel> StatusSegments { get; } = new();
    /// <summary>Распределение приоритетов.</summary>
    public ObservableCollection<PriorityBarViewModel> PriorityBars { get; } = new();
    /// <summary>Строки легенды секторной диаграммы (цвет, название, число).</summary>
    public ObservableCollection<LegendItemViewModel> StatusLegendItems { get; } = new();
    /// <summary>Текст в центре диаграммы: процент выполненных задач.</summary>
    public string DonutCenterText { get; private set; } = "0%";
    /// <summary>Признак отсутствия завершений с известной датой.</summary>
    public bool IsCompletionEmpty { get => _isCompletionEmpty; private set { _isCompletionEmpty = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompletionEmpty))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasCompletionData))); } }
    /// <summary>Признак, показывающий график при наличии сохранённых завершений.</summary>
    public bool HasCompletionData => !IsCompletionEmpty;
    public new event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Выбирает период семи дней.</summary>
    private void PeriodSevenClick(object? sender, RoutedEventArgs e) { _period = AnalyticsService.Period.SevenDays; Rebuild(); }
    /// <summary>Выбирает период тридцати дней.</summary>
    private void PeriodThirtyClick(object? sender, RoutedEventArgs e) { _period = AnalyticsService.Period.ThirtyDays; Rebuild(); }
    /// <summary>Выбирает весь доступный период.</summary>
    private void PeriodAllClick(object? sender, RoutedEventArgs e) { _period = AnalyticsService.Period.AllTime; Rebuild(); }

    /// <summary>Переводит показатели и графики при изменении языка.</summary>
    private void LanguageChanged(object? sender, EventArgs e) => Rebuild();

    /// <summary>Получает актуальные задачи и заново строит все связанные графики.</summary>
    private void Rebuild()
    {
        AnalyticsSnapshot snapshot = _analytics.Build(_tasks.GetAll(), _period, DateTime.Now);
        Statistics.Clear();
        AddStatistic("analytics.total", snapshot.Total);
        AddStatistic("analytics.done", snapshot.Done);
        AddStatistic("analytics.remaining", snapshot.Remaining);
        AddStatistic("analytics.overdue", snapshot.Overdue);
        AddStatistic("analytics.inProgress", snapshot.InProgress);
        AddStatistic("analytics.percent", $"{snapshot.CompletionPercent:0}%");
        AddStatistic("analytics.onTime", snapshot.CompletedOnTime);
        AddStatistic("analytics.late", snapshot.CompletedLate);

        CompletionBars.Clear();
        int maximum = Math.Max(1, snapshot.CompletionDays.Count == 0 ? 0 : System.Linq.Enumerable.Max(snapshot.CompletionDays, day => day.Count));
        foreach (AnalyticsDay day in snapshot.CompletionDays)
            CompletionBars.Add(new CompletionBarViewModel(day.Date, day.Count, maximum));
        IsCompletionEmpty = snapshot.CompletionDays.Count == 0;

        StatusSegments.Clear();
        int total = Math.Max(1, snapshot.Total);
        double start = 0;
        AddSegment(ref start, snapshot.NewCount, total, "#4B9CD3");
        AddSegment(ref start, snapshot.InProgress, total, "#5865D9");
        AddSegment(ref start, snapshot.Done, total, "#237A58");
        AddSegment(ref start, snapshot.Overdue, total, "#B54748");
        StatusLegendItems.Clear();
        StatusLegendItems.Add(new LegendItemViewModel(Loc.T("analytics.legend.new"), snapshot.NewCount, "#4B9CD3"));
        StatusLegendItems.Add(new LegendItemViewModel(Loc.T("analytics.legend.progress"), snapshot.InProgress, "#5865D9"));
        StatusLegendItems.Add(new LegendItemViewModel(Loc.T("analytics.legend.done"), snapshot.Done, "#237A58"));
        StatusLegendItems.Add(new LegendItemViewModel(Loc.T("analytics.legend.overdue"), snapshot.Overdue, "#B54748"));
        DonutCenterText = $"{snapshot.CompletionPercent:0}%";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DonutCenterText)));

        PriorityBars.Clear();
        int priorityMaximum = Math.Max(1, System.Linq.Enumerable.Max(snapshot.Priorities.Values));
        PriorityBars.Add(new PriorityBarViewModel(Loc.T("enum.priority.low"), snapshot.Priorities[Priority.Low], priorityMaximum, "#4B9CD3"));
        PriorityBars.Add(new PriorityBarViewModel(Loc.T("enum.priority.medium"), snapshot.Priorities[Priority.Medium], priorityMaximum, "#D99A2B"));
        PriorityBars.Add(new PriorityBarViewModel(Loc.T("enum.priority.high"), snapshot.Priorities[Priority.High], priorityMaximum, "#D9534F"));
    }

    /// <summary>Добавляет локализованный показатель в плиточную статистику.</summary>
    private void AddStatistic(string key, object value) => Statistics.Add(new StatisticViewModel(Loc.T(key), value.ToString() ?? string.Empty));

    /// <summary>Добавляет сектор и переносит начало следующего сектора по окружности.</summary>
    private void AddSegment(ref double start, int count, int total, string color)
    {
        double fraction = count / (double)total;
        StatusSegments.Add(new DonutSegmentViewModel(start, fraction, color));
        start += fraction * Math.PI * 2;
    }
}

/// <summary>Подпись и значение одной плитки аналитики.</summary>
public sealed record StatisticViewModel(string Label, string Value);
