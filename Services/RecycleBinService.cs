using System;
using System.Collections.Generic;
using System.Linq;
using TaskManager.Models;

namespace TaskManager.Services;

public sealed class RecycleBinService
{
    private readonly RecycleBinStorage _storage;
    private readonly List<DeletedTask> _entries = new();

    public RecycleBinService(RecycleBinStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
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
        _entries.Remove(Find(id));
        Save();
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    private DeletedTask Find(Guid id)
        => _entries.FirstOrDefault(e => e.Task.Id == id)
           ?? throw new KeyNotFoundException(Loc.T("err.bin.notFound"));

    private void Save() => _storage.Save(_entries);
}
