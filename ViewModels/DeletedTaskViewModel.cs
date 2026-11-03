using System;
using Avalonia.Media;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.ViewModels;

public sealed class DeletedTaskViewModel
{
    public DeletedTaskViewModel(DeletedTask entry) => Entry = entry;
    public DeletedTask Entry { get; }
    public TaskItem Task => Entry.Task;
    public Guid Id => Task.Id;
    public string Title => Task.Title;
    public string DueDateText => Task.DueDate.ToString("dd.MM.yyyy, HH:mm");
    public string PriorityText => Task.Priority.ToRussian();
    public string TypeText => Task.GetTypeName();
    public string DeletedAtText => Entry.DeletedAt.ToString("dd.MM.yyyy, HH:mm");
    public IBrush PriorityBrush => Task.Priority switch
    {
        Priority.High => Brush.Parse("#D9534F"),
        Priority.Medium => Brush.Parse("#D99A2B"),
        _ => Brush.Parse("#4B9CD3")
    };
}
