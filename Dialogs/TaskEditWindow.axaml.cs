using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using TaskManager.Models;

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

        PriorityBox.ItemsSource = Enum.GetValues<Priority>();
        StateBox.ItemsSource = Enum.GetValues<TaskState>();
        TypeBox.ItemsSource = new[] { "Обычная", "Повторяющаяся", "Проектная" };
        TypeBox.SelectionChanged += TypeChanged;

        HeaderText.Text = task == null ? "Новая задача" : "Редактирование задачи";
        TitleBox.Text = task?.Title ?? string.Empty;
        DescriptionBox.Text = task?.Description ?? string.Empty;
        DueDatePicker.SelectedDate = task == null
            ? DateTimeOffset.Now.Date
            : new DateTimeOffset(task.DueDate.Date);
        DueTimePicker.SelectedTime = task?.DueDate.TimeOfDay ?? new TimeSpan(23, 59, 0);
        PriorityBox.SelectedItem = task?.Priority ?? Priority.Medium;
        StateBox.SelectedItem = task?.State ?? TaskState.New;
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
        TitleBox.Background = Brushes.Transparent;
        DueDatePicker.Background = Brushes.Transparent;
        RepeatBox.Background = Brushes.Transparent;
        ProjectBox.Background = Brushes.Transparent;
    }

    private void SaveClick(object? sender, RoutedEventArgs e)
    {
        ClearErrors();
        bool isValid = true;

        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            TitleError.Text = "Введите название задачи.";
            TitleBox.Background = Brushes.MistyRose;
            isValid = false;
        }
        else if (TitleBox.Text.Trim().Length > TaskItem.MaxTitleLength)
        {
            TitleError.Text = $"Название не должно превышать {TaskItem.MaxTitleLength} символов.";
            TitleBox.Background = Brushes.MistyRose;
            isValid = false;
        }

        if (!DueDatePicker.SelectedDate.HasValue)
        {
            DueDateError.Text = "Укажите срок выполнения.";
            DueDatePicker.Background = Brushes.MistyRose;
            isValid = false;
        }
        if (!DueTimePicker.SelectedTime.HasValue)
        {
            DueTimeError.Text = "Укажите время выполнения.";
            DueTimePicker.Background = Brushes.MistyRose;
            isValid = false;
        }
        else if (_original == null && DueDatePicker.SelectedDate!.Value.Date.Add(DueTimePicker.SelectedTime ?? TimeSpan.Zero) < DateTime.Now)
        {
            DueDateError.Text = "Срок новой задачи не может быть в прошлом.";
            DueDatePicker.Background = Brushes.MistyRose;
            isValid = false;
        }

        int repeatEveryDays = (int)(RepeatBox.Value ?? 0);
        if (TypeBox.SelectedIndex == 1 && (repeatEveryDays < 1 || repeatEveryDays > 365))
        {
            RepeatError.Text = "Интервал должен быть от 1 до 365 дней.";
            RepeatBox.Background = Brushes.MistyRose;
            isValid = false;
        }

        if (TypeBox.SelectedIndex == 2 && string.IsNullOrWhiteSpace(ProjectBox.Text))
        {
            ProjectError.Text = "Введите название проекта.";
            ProjectBox.Background = Brushes.MistyRose;
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
                Priority = (Priority)PriorityBox.SelectedItem!,
                State = (TaskState)StateBox.SelectedItem!,
                RepeatEveryDays = repeatEveryDays
            },
            2 => new ProjectTask
            {
                Id = _original?.Id ?? Guid.NewGuid(),
                CreatedAt = _original?.CreatedAt ?? DateTime.Now,
                Title = TitleBox.Text!.Trim(),
                Description = DescriptionBox.Text ?? string.Empty,
                DueDate = dueDate,
                Priority = (Priority)PriorityBox.SelectedItem!,
                State = (TaskState)StateBox.SelectedItem!,
                ProjectName = ProjectBox.Text!.Trim()
            },
            _ => new TaskItem
            {
                Id = _original?.Id ?? Guid.NewGuid(),
                CreatedAt = _original?.CreatedAt ?? DateTime.Now,
                Title = TitleBox.Text!.Trim(),
                Description = DescriptionBox.Text ?? string.Empty,
                DueDate = dueDate,
                Priority = (Priority)PriorityBox.SelectedItem!,
                State = (TaskState)StateBox.SelectedItem!
            }
        };

        Close(result);
    }

    private void CancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
