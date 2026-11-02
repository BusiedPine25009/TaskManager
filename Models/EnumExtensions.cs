namespace TaskManager.Models;

/// <summary>
/// Методы-расширения для вывода перечислений на русском языке.
/// Нужны, чтобы в интерфейсе пользователь видел «Высокий», а не «High».
/// </summary>
public static class EnumExtensions
{
    /// <summary>Возвращает русское название приоритета.</summary>
    /// <param name="priority">Приоритет задачи.</param>
    public static string ToRussian(this Priority priority)
    {
        return priority switch
        {
            Priority.Low => "Низкий",
            Priority.Medium => "Средний",
            Priority.High => "Высокий",
            _ => priority.ToString()
        };
    }

    /// <summary>Возвращает русское название статуса.</summary>
    /// <param name="state">Статус задачи.</param>
    public static string ToRussian(this TaskState state)
    {
        return state switch
        {
            TaskState.New => "Новая",
            TaskState.InProgress => "В работе",
            TaskState.Done => "Выполнена",
            _ => state.ToString()
        };
    }
}
