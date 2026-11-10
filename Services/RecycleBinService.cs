using System;
using System.Collections.Generic;
using System.Linq;
using TaskManager.Models;

namespace TaskManager.Services;

public sealed class RecycleBinService
{
    private readonly RecycleBinStorage _storage;
    private readonly AttachmentService? _attachments;
    private readonly List<DeletedTask> _entries = new();

    public RecycleBinService(RecycleBinStorage storage, AttachmentService? attachments = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _attachments = attachments;
    }

    public int Count => _entries.Count;
    public string FilePath => _storage.FilePath;
    public IReadOnlyList<DeletedTask> Entries => _entries;

    public void Load()
    {
        _entries.Clear();
        _entries.AddRange(_storage.Load());
    }

    public void Add(TaskItem task)
    {
        _entries.Insert(0, new DeletedTask { Task = task, DeletedAt = DateTime.Now });
        Save();
    }

    public TaskItem Restore(Guid id)
    {
        DeletedTask entry = Find(id);
        _entries.Remove(entry);
        Save();
        return entry.Task;
    }

    public TaskItem Get(Guid id) => Find(id).Task;

    public void RemoveRestored(Guid id)
    {
        _entries.Remove(Find(id));
        Save();
    }

    public void DeletePermanently(Guid id)
    {
        DeletedTask entry = Find(id);
        _attachments?.DeleteAll(entry.Task);
        _entries.Remove(entry);
        Save();
    }

    public void Clear()
    {
        foreach (DeletedTask entry in _entries)
            _attachments?.DeleteAll(entry.Task);
        _entries.Clear();
        Save();
    }

    private DeletedTask Find(Guid id)
        => _entries.FirstOrDefault(e => e.Task.Id == id)
           ?? throw new KeyNotFoundException(Loc.T("err.bin.notFound"));

    private void Save() => _storage.Save(_entries);
}
