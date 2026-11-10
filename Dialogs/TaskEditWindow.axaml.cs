using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.ViewModels;

namespace TaskManager.Dialogs;

public partial class TaskEditWindow : Window
{
    private readonly TaskItem? _original;
    private readonly AttachmentService _attachmentService;
    private readonly Guid _taskId;
    private readonly List<TaskAttachment> _newAttachments = new();
    private readonly HashSet<Guid> _initialAttachmentIds;

    public TaskEditWindow() : this(null, null, null)
    {
    }

    public TaskEditWindow(TaskItem? task = null, DateTime? suggestedDueDate = null, AttachmentService? attachmentService = null)
    {
        InitializeComponent();
        _original = task;
        _attachmentService = attachmentService ?? new AttachmentService();
        _taskId = task?.Id ?? Guid.NewGuid();
        _initialAttachmentIds = task?.Attachments.Select(attachment => attachment.Id).ToHashSet() ?? new HashSet<Guid>();
        DataContext = this;

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
            ? (suggestedDueDate.HasValue ? new DateTimeOffset(suggestedDueDate.Value.Date) : DateTimeOffset.Now.Date)
            : new DateTimeOffset(task.DueDate.Date);
        DueTimePicker.SelectedTime = task?.DueDate.TimeOfDay ?? suggestedDueDate?.TimeOfDay ?? new TimeSpan(23, 59, 0);
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

        foreach (TaskAttachment attachment in task?.Attachments ?? Enumerable.Empty<TaskAttachment>())
            Attachments.Add(new AttachmentViewModel(attachment));
        ReminderCheck.IsChecked = task?.ReminderAt.HasValue ?? false;
        ReminderDatePicker.SelectedDate = task?.ReminderAt.HasValue == true
            ? new DateTimeOffset(task.ReminderAt.Value.Date) : DateTimeOffset.Now.Date;
        ReminderTimePicker.SelectedTime = task?.ReminderAt?.TimeOfDay ?? new TimeSpan(9, 0, 0);

        UpdateTypeFields();
        UpdateReminderFields();
    }

    /// <summary>Вложения, которые попадут в JSON вместе с сохранённой задачей.</summary>
    public ObservableCollection<AttachmentViewModel> Attachments { get; } = new();

    private void TypeChanged(object? sender, SelectionChangedEventArgs e) => UpdateTypeFields();

    private void UpdateTypeFields()
    {
        RecurringPanel.IsVisible = TypeBox.SelectedIndex == 1;
        ProjectPanel.IsVisible = TypeBox.SelectedIndex == 2;
    }

    /// <summary>Показывает дату и время только для включённого напоминания.</summary>
    private void ReminderChanged(object? sender, RoutedEventArgs e) => UpdateReminderFields();

    /// <summary>Синхронизирует видимость полей напоминания с флажком.</summary>
    private void UpdateReminderFields() => ReminderFields.IsVisible = ReminderCheck.IsChecked == true;

    /// <summary>Открывает системный выбор файлов и создаёт независимые внутренние копии.</summary>
    private async void AttachFileClick(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("attachment.pickerTitle"),
            AllowMultiple = true
        });
        foreach (IStorageFile file in files)
        {
            string? path = file.TryGetLocalPath();
            if (path == null)
            {
                await new MessageDialog(Loc.T("common.error"), Loc.T("attachment.err.localOnly")).ShowDialog(this);
                continue;
            }
            try
            {
                TaskAttachment attachment = _attachmentService.Add(_taskId, path);
                _newAttachments.Add(attachment);
                Attachments.Add(new AttachmentViewModel(attachment));
            }
            catch (Exception ex)
            {
                await new MessageDialog(Loc.T("common.error"), ex.Message).ShowDialog(this);
            }
        }
    }

    /// <summary>Открывает сохранённую копию выбранного вложения системным приложением.</summary>
    private async void OpenAttachmentClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not AttachmentViewModel item) return;
        try { _attachmentService.Open(item.Attachment); }
        catch (Exception ex) { await new MessageDialog(Loc.T("common.error"), ex.Message).ShowDialog(this); }
    }

    /// <summary>Убирает вложение из будущего сохранения и удаляет только новую внутреннюю копию.</summary>
    private void RemoveAttachmentClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not AttachmentViewModel item) return;
        if (_newAttachments.Remove(item.Attachment))
            _attachmentService.Delete(item.Attachment);
        Attachments.Remove(item);
    }

    private void ClearErrors()
    {
        TitleError.Text = string.Empty;
        DueDateError.Text = string.Empty;
        DueTimeError.Text = string.Empty;
        RepeatError.Text = string.Empty;
        ProjectError.Text = string.Empty;
        ReminderError.Text = string.Empty;
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

        DateTime? reminderAt = null;
        if (ReminderCheck.IsChecked == true)
        {
            if (!ReminderDatePicker.SelectedDate.HasValue || !ReminderTimePicker.SelectedTime.HasValue)
            {
                ReminderError.Text = Loc.T("reminder.err.required");
                isValid = false;
            }
            else
            {
                reminderAt = ReminderDatePicker.SelectedDate.Value.Date.Add(ReminderTimePicker.SelectedTime.Value);
                if (reminderAt.Value < DateTime.Now)
                {
                    ReminderError.Text = Loc.T("reminder.err.past");
                    isValid = false;
                }
            }
        }

        if (!isValid)
            return;

        DateTime dueDate = DueDatePicker.SelectedDate!.Value.Date
            .Add(DueTimePicker.SelectedTime!.Value);
        TaskItem result = TypeBox.SelectedIndex switch
        {
            1 => new RecurringTask
            {
                Id = _taskId,
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
                Id = _taskId,
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
                Id = _taskId,
                CreatedAt = _original?.CreatedAt ?? DateTime.Now,
                Title = TitleBox.Text!.Trim(),
                Description = DescriptionBox.Text ?? string.Empty,
                DueDate = dueDate,
                Priority = SelectedPriority,
                State = SelectedState
            }
        };

        result.Attachments = Attachments.Select(item => item.Attachment).ToList();
        result.ReminderAt = result.State == TaskState.Done ? null : reminderAt;
        DateTime? previousReminderSentAt = _original?.ReminderSentAt;
        result.ReminderSentAt = _original?.ReminderAt == result.ReminderAt ? previousReminderSentAt : null;
        result.CompletedAt = result.State == TaskState.Done ? _original?.CompletedAt ?? DateTime.Now : null;
        Close(result);
    }

    /// <summary>Удаляет только созданные в этом диалоге копии и закрывает его без изменения задачи.</summary>
    private void CancelClick(object? sender, RoutedEventArgs e)
    {
        foreach (TaskAttachment attachment in _newAttachments)
            _attachmentService.Delete(attachment);
        Close(null);
    }

    // Выбранный в списке приоритет и статус
    private Priority SelectedPriority => ((Option<Priority>)PriorityBox.SelectedItem!).Value;
    private TaskState SelectedState => ((Option<TaskState>)StateBox.SelectedItem!).Value;

    // Пункт выпадающего списка: показывает русскую подпись, хранит значение перечисления
    private sealed record Option<T>(string Label, T Value)
    {
        public override string ToString() => Label;
    }
}
