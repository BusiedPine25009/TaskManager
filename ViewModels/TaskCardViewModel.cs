using Avalonia.Media;
using System;
using TaskManager.Models;

namespace TaskManager.ViewModels;

public sealed class TaskCardViewModel
{
    public TaskCardViewModel(TaskItem task)
    {
        Task = task;
    }

    public TaskItem Task { get; }
    public string Title => Task.Title;
    public string Description => Task.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Task.Description);
    public string DueDateText => $"Срок: {Task.DueDate:dd.MM.yyyy, HH:mm}";
    public string RemainingText => Task.State == TaskState.Done
        ? "Выполнена"
        : FormatRemaining(Task.DueDate - DateTime.Now);
    public string PriorityText => Task.Priority.ToRussian();
    public string StateText => Task.State.ToRussian();
    public string TypeText => Task.GetTypeName();
    public string DetailsText => Task switch
    {
        RecurringTask recurring => $"Повтор каждые {recurring.RepeatEveryDays} дн.",
        ProjectTask project => $"Проект: {project.ProjectName}",
        _ => string.Empty
    };
    public bool HasDetails => !string.IsNullOrEmpty(DetailsText);
    public bool IsOverdue => Task.IsOverdue;
    public bool IsDone => Task.State == TaskState.Done;
    public bool CanMarkDone => !IsDone || Task is RecurringTask;
    public IBrush PriorityBrush => Task.Priority switch
    {
        Priority.High => Brush.Parse("#D9534F"),
        Priority.Medium => Brush.Parse("#D99A2B"),
        _ => Brush.Parse("#4B9CD3")
    };
    public IBrush StateBrush => Task.State switch
    {
        TaskState.Done => Brush.Parse("#3D8B68"),
        TaskState.InProgress => Brush.Parse("#6B5CC5"),
        _ => Brush.Parse("#4B9CD3")
    };

    private static string FormatRemaining(TimeSpan remaining)
    {
        bool overdue = remaining < TimeSpan.Zero;
        TimeSpan value = overdue ? remaining.Duration() : remaining;
        string text = value.TotalDays >= 1
            ? $"{value.Days} {Plural(value.Days, "день", "дня", "дней")} {value.Hours} {Plural(value.Hours, "час", "часа", "часов")}"
            : value.TotalHours >= 1
                ? $"{value.Hours} {Plural(value.Hours, "час", "часа", "часов")} {value.Minutes} {Plural(value.Minutes, "минута", "минуты", "минут")}"
                : $"{Math.Max(1, value.Minutes)} {Plural(Math.Max(1, value.Minutes), "минута", "минуты", "минут")}";
        return overdue ? $"Просрочена на {text}" : $"Осталось {text}";
    }

    private static string Plural(int value, string one, string few, string many)
    {
        int lastTwo = value % 100;
        return lastTwo is >= 11 and <= 14 ? many : (value % 10) switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many
        };
    }
}
