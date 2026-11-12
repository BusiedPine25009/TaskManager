using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.ViewModels;

public sealed class DeletedTaskViewModel : INotifyPropertyChanged, IDisposable
{
    public DeletedTaskViewModel(DeletedTask entry)
    {
        Entry = entry;
        Loc.Instance.LanguageChanged += LanguageChanged;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public DeletedTask Entry { get; }
    public TaskItem Task => Entry.Task;
    public Guid Id => Task.Id;
    public string Title => Task.Title;
    public string DueDateText => Task.DueDate.ToString("dd.MM.yyyy, HH:mm", CultureInfo.InvariantCulture);
    public string PriorityText => Task.Priority.ToDisplayName();
    public string TypeText => Task.GetTypeName();
    public string DeletedAtText => Entry.DeletedAt.ToString("dd.MM.yyyy, HH:mm", CultureInfo.InvariantCulture);
    public IBrush PriorityBrush => Task.Priority switch
    {
        Priority.High => Brush.Parse("#D9534F"),
        Priority.Medium => Brush.Parse("#D99A2B"),
        _ => Brush.Parse("#4B9CD3")
    };

    /// <summary>Обновляет системные подписи удалённой задачи после смены языка.</summary>
    private void LanguageChanged(object? sender, EventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PriorityText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TypeText)));
    }

    /// <summary>Освобождает подписку карточки корзины на смену языка.</summary>
    public void Dispose() => Loc.Instance.LanguageChanged -= LanguageChanged;
}
