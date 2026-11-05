using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.Dialogs;

public partial class TaskEditWindow : Window
{
    private readonly TaskItem? _original;

    public TaskEditWindow() : this(null)
    {
    }

    public TaskEditWindow(TaskItem? task = null)
    {
        InitializeComponent();
        _original = task;

        // В списках показываем русские названия, а в Value храним само значение перечисления
        var priorities = Enum.GetValues<Priority>().Select(p => new Option<Priority>(p.ToDisplayName(), p)).ToList();
        var states = Enum.GetValues<TaskState>().Select(s => new Option<TaskState>(s.ToDisplayName(), s)).ToList();
        PriorityBox.ItemsSource = priorities;
        StateBox.ItemsSource = states;
        TypeBox.ItemsSource = new[]
        {
            Loc.T("enum.type.basic"), Loc.T("enum.type.recurring"), Loc.T("enum.type.project")
        };
        TypeBox.SelectionChanged += TypeChanged;

        HeaderText.Text = task == null ? Loc.T("edit.newTitle") : Loc.T("edit.editTitle");
        TitleBox.Text = task?.Title ?? string.Empty;
        DescriptionBox.Text = task?.Description ?? string.Empty;
        DueDatePicker.SelectedDate = task == null
            ? DateTimeOffset.Now.Date
            : new DateTimeOffset(task.DueDate.Date);
        DueTimePicker.SelectedTime = task?.DueDate.TimeOfDay ?? new TimeSpan(23, 59, 0);
        PriorityBox.SelectedItem = priorities.First(o => o.Value == (task?.Priority ?? Priority.Medium));
        StateBox.SelectedItem = states.First(o => o.Value == (task?.State ?? TaskState.New));
        TypeBox.SelectedIndex = task switch
        {
            RecurringTask => 1,
            ProjectTask => 2,
            _ => 0
        };

        if (task is RecurringTask recurring)
            RepeatBox.Value = recurring.RepeatEveryDays;
        if (task is ProjectTask project)
            ProjectBox.Text = project.ProjectName;

        UpdateTypeFields();
    }

    private void TypeChanged(object? sender, SelectionChangedEventArgs e) => UpdateTypeFields();

    private void UpdateTypeFields()
    {
        RecurringPanel.IsVisible = TypeBox.SelectedIndex == 1;
        ProjectPanel.IsVisible = TypeBox.SelectedIndex == 2;
    }

    private void ClearErrors()
    {
        TitleError.Text = string.Empty;
        DueDateError.Text = string.Empty;
        DueTimeError.Text = string.Empty;
        RepeatError.Text = string.Empty;
        ProjectError.Text = string.Empty;
        TitleBox.Classes.Remove("invalid");
        DueDatePicker.Classes.Remove("invalid");
        DueTimePicker.Classes.Remove("invalid");
        RepeatBox.Classes.Remove("invalid");
        ProjectBox.Classes.Remove("invalid");
    }

    private void SaveClick(object? sender, RoutedEventArgs e)
    {
        ClearErrors();
        bool isValid = true;

        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            TitleError.Text = Loc.T("edit.err.title");
            TitleBox.Classes.Add("invalid");
            isValid = false;
        }
        else if (TitleBox.Text.Trim().Length > TaskItem.MaxTitleLength)
        {
            TitleError.Text = Loc.T("edit.err.titleLong", TaskItem.MaxTitleLength);
            TitleBox.Classes.Add("invalid");
            isValid = false;
        }

        if (!DueDatePicker.SelectedDate.HasValue)
        {
            DueDateError.Text = Loc.T("edit.err.date");
            DueDatePicker.Classes.Add("invalid");
            isValid = false;
        }
        if (!DueTimePicker.SelectedTime.HasValue)
        {
            DueTimeError.Text = Loc.T("edit.err.time");
            DueTimePicker.Classes.Add("invalid");
            isValid = false;
        }
        else if (_original == null && DueDatePicker.SelectedDate!.Value.Date.Add(DueTimePicker.SelectedTime ?? TimeSpan.Zero) < DateTime.Now)
        {
            DueDateError.Text = Loc.T("edit.err.past");
            DueDatePicker.Classes.Add("invalid");
            isValid = false;
        }

        int repeatEveryDays = (int)(RepeatBox.Value ?? 0);
        if (TypeBox.SelectedIndex == 1 && (repeatEveryDays < 1 || repeatEveryDays > 365))
        {
            RepeatError.Text = Loc.T("edit.err.repeat");
            RepeatBox.Classes.Add("invalid");
            isValid = false;
        }

        if (TypeBox.SelectedIndex == 2 && string.IsNullOrWhiteSpace(ProjectBox.Text))
        {
            ProjectError.Text = Loc.T("edit.err.project");
            ProjectBox.Classes.Add("invalid");
            isValid = false;
        }

        if (!isValid)
            return;

        DateTime dueDate = DueDatePicker.SelectedDate!.Value.Date
            .Add(DueTimePicker.SelectedTime!.Value);
        TaskItem result = TypeBox.SelectedIndex switch
        {
            1 => new RecurringTask
            {
                Id = _original?.Id ?? Guid.NewGuid(),
                CreatedAt = _original?.CreatedAt ?? DateTime.Now,
                Title = TitleBox.Text!.Trim(),
                Description = DescriptionBox.Text ?? string.Empty,
                DueDate = dueDate,
                Priority = SelectedPriority,
                State = SelectedState,
                RepeatEveryDays = repeatEveryDays
            },
            2 => new ProjectTask
            {
                Id = _original?.Id ?? Guid.NewGuid(),
                CreatedAt = _original?.CreatedAt ?? DateTime.Now,
                Title = TitleBox.Text!.Trim(),
                Description = DescriptionBox.Text ?? string.Empty,
                DueDate = dueDate,
                Priority = SelectedPriority,
                State = SelectedState,
                ProjectName = ProjectBox.Text!.Trim()
            },
            _ => new TaskItem
            {
                Id = _original?.Id ?? Guid.NewGuid(),
                CreatedAt = _original?.CreatedAt ?? DateTime.Now,
                Title = TitleBox.Text!.Trim(),
                Description = DescriptionBox.Text ?? string.Empty,
                DueDate = dueDate,
                Priority = SelectedPriority,
                State = SelectedState
            }
        };

        Close(result);
    }

    private void CancelClick(object? sender, RoutedEventArgs e) => Close(null);

    // Выбранный в списке приоритет и статус
    private Priority SelectedPriority => ((Option<Priority>)PriorityBox.SelectedItem!).Value;
    private TaskState SelectedState => ((Option<TaskState>)StateBox.SelectedItem!).Value;

    // Пункт выпадающего списка: показывает русскую подпись, хранит значение перечисления
    private sealed record Option<T>(string Label, T Value)
    {
        public override string ToString() => Label;
    }
}
