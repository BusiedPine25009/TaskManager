using TaskManager.Services;

namespace TaskManager.Models;

/// <summary>
/// Методы-расширения для получения локализованных подписей перечислений.
/// Сами значения enum остаются прежними и продолжают сохраняться в данных без изменений.
/// </summary>
public static class EnumExtensions
{
    /// <summary>Возвращает локализованное название приоритета.</summary>
    /// <param name="priority">Приоритет задачи.</param>
    public static string ToDisplayName(this Priority priority)
    {
        return priority switch
        {
            Priority.Low => Loc.T("enum.priority.low"),
            Priority.Medium => Loc.T("enum.priority.medium"),
            Priority.High => Loc.T("enum.priority.high"),
            _ => priority.ToString()
        };
    }

    /// <summary>Возвращает локализованное название статуса.</summary>
    /// <param name="state">Статус задачи.</param>
    public static string ToDisplayName(this TaskState state)
    {
        return state switch
        {
            TaskState.New => Loc.T("enum.state.new"),
            TaskState.InProgress => Loc.T("enum.state.inProgress"),
            TaskState.Done => Loc.T("enum.state.done"),
            _ => state.ToString()
        };
    }
}
