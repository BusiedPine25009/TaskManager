namespace TaskManager.Models;

/// <summary>
/// Статус выполнения задачи.
/// Назван TaskState, а не TaskStatus, чтобы не конфликтовать
/// со стандартным типом System.Threading.Tasks.TaskStatus.
/// </summary>
public enum TaskState
{
    /// <summary>Задача создана, работа не начата.</summary>
    New = 0,

    /// <summary>Задача в работе.</summary>
    InProgress = 1,

    /// <summary>Задача выполнена.</summary>
    Done = 2
}
