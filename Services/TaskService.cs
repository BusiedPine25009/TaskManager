using System;
using System.Collections.Generic;
using System.Linq;
using TaskManager.Models;

namespace TaskManager.Services;

/// <summary>
/// Слой бизнес-логики: хранит список задач в памяти и реализует все операции
/// (добавление, удаление, изменение, поиск, фильтрация). Интерфейс обращается
/// только к этому классу, а сохранение делегирует JsonTaskStorage.
/// </summary>
public class TaskService
{
    // Список задач в памяти. Снаружи напрямую недоступен (инкапсуляция).
    private readonly List<TaskItem> _tasks = new();

    // Хранилище данных, внедряется через конструктор.
    private readonly JsonTaskStorage _storage;
    private readonly RecycleBinService? _recycleBin;

    /// <summary>Создаёт сервис с указанным хранилищем.</summary>
    /// <param name="storage">Объект, который сохраняет и загружает задачи.</param>
    public TaskService(JsonTaskStorage storage)
        : this(storage, null)
    {
    }

    public TaskService(JsonTaskStorage storage, RecycleBinService? recycleBin)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _recycleBin = recycleBin;
    }

    /// <summary>Общее количество задач.</summary>
    public int TotalCount => _tasks.Count;

    /// <summary>Количество выполненных задач.</summary>
    public int DoneCount => _tasks.Count(t => t.State == TaskState.Done);

    /// <summary>Количество просроченных задач.</summary>
    public int OverdueCount => _tasks.Count(t => t.IsOverdue);

    /// <summary>
    /// Загружает задачи из файла в память (вызывается при запуске программы).
    /// Может выбросить InvalidDataException, если файл повреждён.
    /// </summary>
    public void Load()
    {
        List<TaskItem> loaded = _storage.Load();
        _tasks.Clear();
        _tasks.AddRange(loaded);
    }

    public void RestoreFromBackup()
    {
        List<TaskItem> loaded = _storage.LoadBackup();
        _tasks.Clear();
        _tasks.AddRange(loaded);
        Save();
    }

    /// <summary>
    /// Добавляет новую задачу и сразу сохраняет данные.
    /// </summary>
    /// <param name="task">Новая задача.</param>
    /// <exception cref="ArgumentException">Срок выполнения в прошлом.</exception>
    public void Add(TaskItem task)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));

        // Название уже проверено в свойстве Title; здесь проверяем срок.
        // Для старых задач при редактировании это правило не действует.
        if (task.DueDate < DateTime.Now)
            throw new ArgumentException("Срок выполнения новой задачи не может быть в прошлом.");

        _tasks.Add(task);
        Save();
    }

    /// <summary>
    /// Удаляет задачу по идентификатору и сохраняет данные.
    /// </summary>
    /// <param name="id">Идентификатор задачи.</param>
    /// <exception cref="KeyNotFoundException">Задача не найдена.</exception>
    public void Remove(Guid id)
    {
        TaskItem task = FindOrThrow(id);
        _tasks.Remove(task);
        _recycleBin?.Add(task);
        Save();
    }

    public void Restore(TaskItem task)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));
        if (_tasks.Any(existing => existing.Id == task.Id))
            throw new InvalidOperationException("Задача с таким идентификатором уже существует.");
        _tasks.Add(task);
        Save();
    }

    /// <summary>
    /// Изменяет основные поля задачи и сохраняет данные.
    /// Название проверяется первым: если оно некорректно, остальные поля не меняются.
    /// </summary>
    public void Update(Guid id, string title, string description, DateTime dueDate, Priority priority, TaskState state)
    {
        TaskItem task = FindOrThrow(id);

        task.Title = title;               // может выбросить ArgumentException
        task.Description = description ?? string.Empty;
        task.DueDate = dueDate;
        task.Priority = priority;
        task.State = state;

        Save();
    }

    /// <summary>
    /// Заменяет задачу, сохраняя её идентификатор и дату создания.
    /// Используется интерфейсом при смене вида задачи.
    /// </summary>
    public void Update(TaskItem replacement)
    {
        if (replacement == null)
            throw new ArgumentNullException(nameof(replacement));

        TaskItem current = FindOrThrow(replacement.Id);
        replacement.CreatedAt = current.CreatedAt;

        int index = _tasks.IndexOf(current);
        _tasks[index] = replacement;
        Save();
    }

    /// <summary>
    /// Отмечает задачу выполненной и сохраняет данные.
    /// Для повторяющейся задачи сработает её собственная логика (полиморфизм).
    /// </summary>
    /// <param name="id">Идентификатор задачи.</param>
    public void MarkDone(Guid id)
    {
        TaskItem task = FindOrThrow(id);
        task.MarkDone();
        Save();
    }

    /// <summary>
    /// Возвращает все задачи в удобном порядке: невыполненные сверху,
    /// затем по сроку, затем по приоритету (высокий раньше).
    /// </summary>
    public List<TaskItem> GetAll()
    {
        return Sort(_tasks).ToList();
    }

    /// <summary>
    /// Поиск и фильтрация одним вызовом. Любой параметр можно не указывать (null).
    /// </summary>
    /// <param name="query">Текст для поиска в названии и описании (без учёта регистра).</param>
    /// <param name="state">Показать только задачи с этим статусом.</param>
    /// <param name="priority">Показать только задачи с этим приоритетом.</param>
    public List<TaskItem> Find(string? query, TaskState? state, Priority? priority)
    {
        IEnumerable<TaskItem> result = _tasks;

        if (!string.IsNullOrWhiteSpace(query))
        {
            string text = query.Trim();
            result = result.Where(t =>
                t.Title.Contains(text, StringComparison.CurrentCultureIgnoreCase) ||
                t.Description.Contains(text, StringComparison.CurrentCultureIgnoreCase));
        }

        if (state.HasValue)
            result = result.Where(t => t.State == state.Value);

        if (priority.HasValue)
            result = result.Where(t => t.Priority == priority.Value);

        return Sort(result).ToList();
    }

    /// <summary>Сохраняет текущий список задач в файл.</summary>
    private void Save()
    {
        _storage.Save(_tasks);
    }

    /// <summary>Ищет задачу по идентификатору или выбрасывает исключение.</summary>
    private TaskItem FindOrThrow(Guid id)
    {
        TaskItem? task = _tasks.FirstOrDefault(t => t.Id == id);

        if (task == null)
            throw new KeyNotFoundException("Задача не найдена. Возможно, она уже была удалена.");

        return task;
    }

    /// <summary>Единый порядок сортировки для всех списков.</summary>
    private static IEnumerable<TaskItem> Sort(IEnumerable<TaskItem> source)
    {
        return source
            .OrderBy(t => t.State == TaskState.Done)    // выполненные уходят вниз
            .ThenBy(t => t.DueDate)                      // ближайший срок раньше
            .ThenByDescending(t => t.Priority);          // при равных сроках важные выше
    }
}
