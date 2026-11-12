using System;
using TaskManager.Models;

namespace TaskManager.ViewModels;

/// <summary>Отображаемая задача выбранного дня в календаре.</summary>
public sealed class CalendarTaskViewModel
{
    /// <summary>Создаёт представление задачи без дублирования её данных.</summary>
    public CalendarTaskViewModel(TaskItem task) => Task = task;

    /// <summary>Исходная задача, открываемая существующим редактором.</summary>
    public TaskItem Task { get; }
    /// <summary>Время срока в формате, подходящем для строки календаря.</summary>
    public string TimeText => Task.DueDate.ToString("HH:mm");
    /// <summary>Пользовательское название задачи без перевода.</summary>
    public string Title => Task.Title;
    /// <summary>Локализованный системный статус.</summary>
    public string StateText => Task.State.ToDisplayName();
    /// <summary>Цветовой акцент по важности или состоянию.</summary>
    public string AccentColor => Task.IsOverdue ? "#B54748" : Task.State == TaskState.Done ? "#237A58" :
        Task.Priority == Priority.High ? "#D9534F" : "#5865D9";
}
