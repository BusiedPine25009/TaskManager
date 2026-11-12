using System;
using System.Collections.Generic;
using System.Linq;
using TaskManager.Models;

namespace TaskManager.ViewModels;

/// <summary>Одна ячейка месячного календаря и задачи, срок которых приходится на её дату.</summary>
public sealed class CalendarDayViewModel
{
    /// <summary>Создаёт ячейку календаря для указанного дня и списка задач.</summary>
    public CalendarDayViewModel(DateTime date, DateTime visibleMonth, IEnumerable<TaskItem> tasks, DateTime selectedDate)
    {
        Date = date.Date;
        IsCurrentMonth = Date.Month == visibleMonth.Month && Date.Year == visibleMonth.Year;
        IsToday = Date == DateTime.Today;
        IsSelected = Date == selectedDate.Date;
        Tasks = tasks.Where(task => task.DueDate.Date == Date).OrderBy(task => task.DueDate).ToList();
    }

    /// <summary>Дата ячейки.</summary>
    public DateTime Date { get; }
    /// <summary>Короткий номер дня месяца.</summary>
    public string DayNumber => Date.Day.ToString();
    /// <summary>Принадлежность показываемому месяцу.</summary>
    public bool IsCurrentMonth { get; }
    /// <summary>Признак текущей системной даты.</summary>
    public bool IsToday { get; }
    /// <summary>Признак выбранной пользователем даты.</summary>
    public bool IsSelected { get; }
    /// <summary>Задачи с указанным сроком без создания копий задач.</summary>
    public IReadOnlyList<TaskItem> Tasks { get; }
    /// <summary>Количество задач для компактного бейджа.</summary>
    public int TaskCount => Tasks.Count;
    /// <summary>Есть ли хотя бы одна просроченная задача в ячейке.</summary>
    public bool HasOverdue => Tasks.Any(task => task.IsOverdue);
    /// <summary>Есть ли задача с высоким приоритетом.</summary>
    public bool HasHighPriority => Tasks.Any(task => task.Priority == Priority.High);
    /// <summary>Есть ли выполненная задача.</summary>
    public bool HasDone => Tasks.Any(task => task.State == TaskState.Done);
    /// <summary>День принадлежит соседнему месяцу (такие ячейки показываются бледнее).</summary>
    public bool IsOtherMonth => !IsCurrentMonth;
    /// <summary>Есть ли в ячейке хотя бы одна задача.</summary>
    public bool HasTasks => Tasks.Count > 0;
    /// <summary>Первые задачи дня для показа внутри ячейки (не больше двух).</summary>
    public IReadOnlyList<CalendarChipViewModel> Preview => Tasks.Take(MaxPreview).Select(task => new CalendarChipViewModel(task)).ToList();
    /// <summary>Сколько задач не поместилось в ячейку.</summary>
    public int MoreCount => Math.Max(0, Tasks.Count - MaxPreview);
    /// <summary>Нужно ли показывать подпись «+N».</summary>
    public bool HasMore => MoreCount > 0;
    /// <summary>Подпись о скрытых задачах, например «+3».</summary>
    public string MoreText => "+" + MoreCount;

    private const int MaxPreview = 2;
}

/// <summary>Короткая строка задачи внутри ячейки календаря: цветная полоска и название.</summary>
public sealed class CalendarChipViewModel
{
    /// <summary>Создаёт строку для задачи.</summary>
    public CalendarChipViewModel(TaskItem task)
    {
        Title = task.Title;
        Color = new CalendarTaskViewModel(task).AccentColor;
    }

    /// <summary>Название задачи.</summary>
    public string Title { get; }
    /// <summary>Цвет полоски: просрочено, выполнено, высокий приоритет или обычный.</summary>
    public string Color { get; }
}
