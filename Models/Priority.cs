namespace TaskManager.Models;

/// <summary>
/// Приоритет задачи. Значения упорядочены по возрастанию важности,
/// поэтому их можно сравнивать и сортировать как числа.
/// </summary>
public enum Priority
{
    /// <summary>Низкий приоритет.</summary>
    Low = 0,

    /// <summary>Средний приоритет.</summary>
    Medium = 1,

    /// <summary>Высокий приоритет.</summary>
    High = 2
}
