using System;
using System.Collections.Generic;
using System.Linq;
using TaskManager.Models;

namespace TaskManager.Services;

/// <summary>Вычисляет показатели аналитики только по активным задачам TaskManager.</summary>
public sealed class AnalyticsService
{
    /// <summary>Периоды, доступные в окне аналитики.</summary>
    public enum Period { SevenDays, ThirtyDays, AllTime }

    /// <summary>Создаёт единый срез данных для показателей и графиков выбранного периода.</summary>
    public AnalyticsSnapshot Build(IEnumerable<TaskItem> source, Period period, DateTime now)
    {
        DateTime? from = period switch
        {
            Period.SevenDays => now.Date.AddDays(-6),
            Period.ThirtyDays => now.Date.AddDays(-29),
            _ => null
        };
        List<TaskItem> tasks = source.Where(task => !from.HasValue || task.CreatedAt >= from.Value).ToList();
        int done = tasks.Count(task => task.State == TaskState.Done);
        int overdue = tasks.Count(task => task.State != TaskState.Done && task.DueDate < now);
        int inProgress = tasks.Count(task => task.State == TaskState.InProgress && task.DueDate >= now);
        int newCount = tasks.Count(task => task.State == TaskState.New && task.DueDate >= now);
        int onTime = tasks.Count(task => task.CompletedAt.HasValue && task.CompletedAt <= task.DueDate);
        int late = tasks.Count(task => task.CompletedAt.HasValue && task.CompletedAt > task.DueDate);
        return new AnalyticsSnapshot(tasks.Count, done, tasks.Count - done, overdue, inProgress,
            tasks.Count == 0 ? 0 : Math.Round(done * 100d / tasks.Count), onTime, late,
            newCount, BuildCompletionDays(tasks, period, now), BuildPriorityCounts(tasks));
    }

    /// <summary>Формирует последовательность дней и число завершений по сохранённым отметкам времени.</summary>
    private static IReadOnlyList<AnalyticsDay> BuildCompletionDays(IEnumerable<TaskItem> tasks, Period period, DateTime now)
    {
        int days = period == Period.ThirtyDays ? 30 : 7;
        if (period == Period.AllTime)
        {
            List<TaskItem> completed = tasks.Where(task => task.CompletedAt.HasValue).ToList();
            if (completed.Count == 0) return Array.Empty<AnalyticsDay>();
            DateTime first = completed.Min(task => task.CompletedAt!.Value).Date;
            days = Math.Max(1, (now.Date - first).Days + 1);
        }
        DateTime start = now.Date.AddDays(-(days - 1));
        return Enumerable.Range(0, days).Select(offset =>
        {
            DateTime day = start.AddDays(offset);
            int count = tasks.Count(task => task.CompletedAt?.Date == day);
            return new AnalyticsDay(day, count);
        }).ToList();
    }

    /// <summary>Считает приоритеты; каждая задача относится ровно к одному столбцу.</summary>
    private static IReadOnlyDictionary<Priority, int> BuildPriorityCounts(IEnumerable<TaskItem> tasks)
        => Enum.GetValues<Priority>().ToDictionary(priority => priority, priority => tasks.Count(task => task.Priority == priority));
}

/// <summary>Неподвижный результат расчёта аналитики.</summary>
public sealed record AnalyticsSnapshot(int Total, int Done, int Remaining, int Overdue, int InProgress,
    double CompletionPercent, int CompletedOnTime, int CompletedLate, int NewCount,
    IReadOnlyList<AnalyticsDay> CompletionDays, IReadOnlyDictionary<Priority, int> Priorities);

/// <summary>Одна точка графика завершений.</summary>
public sealed record AnalyticsDay(DateTime Date, int Count);
