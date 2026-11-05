using Avalonia.Media;
using System;
using System.ComponentModel;
using System.Globalization;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.ViewModels;

public sealed class TaskCardViewModel : INotifyPropertyChanged, IDisposable
{
    public TaskCardViewModel(TaskItem task)
    {
        Task = task;
        Loc.Instance.LanguageChanged += LanguageChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TaskItem Task { get; }
    public string Title => Task.Title;
    public string Description => Task.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Task.Description);
    public string DueDateText => Loc.T("card.dueText", Task.DueDate.ToString("dd.MM.yyyy, HH:mm", CultureInfo.InvariantCulture));
    public string RemainingText => Task.State == TaskState.Done
        ? Loc.T("card.remainingDone")
        : FormatRemaining(Task.DueDate - DateTime.Now);
    public string PriorityText => Task.Priority.ToDisplayName();
    public string StateText => Task.State.ToDisplayName();
    public string TypeText => Task.GetTypeName();
    public string DetailsText => Task switch
    {
        RecurringTask recurring => Loc.T("card.repeatEvery", recurring.RepeatEveryDays),
        ProjectTask project => Loc.T("card.project", project.ProjectName),
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

    /// <summary>Формирует локализованную строку оставшегося или просроченного времени.</summary>
    /// <param name="remaining">Разница между сроком задачи и текущим временем.</param>
    private static string FormatRemaining(TimeSpan remaining)
    {
        bool overdue = remaining < TimeSpan.Zero;
        TimeSpan value = overdue ? remaining.Duration() : remaining;
        string text = value.TotalDays >= 1
            ? $"{value.Days} {Loc.Plural("time.day", value.Days)} {value.Hours} {Loc.Plural("time.hour", value.Hours)}"
            : value.TotalHours >= 1
                ? $"{value.Hours} {Loc.Plural("time.hour", value.Hours)} {value.Minutes} {Loc.Plural("time.minute", value.Minutes)}"
                : $"{Math.Max(1, value.Minutes)} {Loc.Plural("time.minute", Math.Max(1, value.Minutes))}";
        return overdue ? Loc.T("card.overdueBy", text) : Loc.T("card.timeLeft", text);
    }

    /// <summary>Обновляет вычисляемые подписи карточки после смены языка интерфейса.</summary>
    private void LanguageChanged(object? sender, EventArgs e)
    {
        foreach (string propertyName in new[]
                 { nameof(DueDateText), nameof(RemainingText), nameof(PriorityText),
                   nameof(StateText), nameof(TypeText), nameof(DetailsText), nameof(HasDetails) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>Отписывает удалённую из списка карточку от глобального события языка.</summary>
    public void Dispose() => Loc.Instance.LanguageChanged -= LanguageChanged;
}
