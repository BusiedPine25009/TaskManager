using System;
using System.Text.Json.Serialization;

namespace TaskManager.Models;

/// <summary>
/// Базовая задача. От неё наследуются RecurringTask и ProjectTask.
/// Атрибуты JsonPolymorphic / JsonDerivedType нужны, чтобы при сохранении в JSON
/// запоминался конкретный тип задачи и при загрузке создавался правильный класс.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TaskItem), "basic")]
[JsonDerivedType(typeof(RecurringTask), "recurring")]
[JsonDerivedType(typeof(ProjectTask), "project")]
public class TaskItem
{
    /// <summary>Максимально допустимая длина названия задачи.</summary>
    public const int MaxTitleLength = 100;

    // Поле для хранения названия. Прямой доступ закрыт (инкапсуляция):
    // значение можно изменить только через свойство Title с проверкой.
    private string _title = string.Empty;

    /// <summary>Уникальный идентификатор задачи.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Название задачи. Не может быть пустым и длиннее MaxTitleLength символов.
    /// При нарушении правил выбрасывает ArgumentException с понятным сообщением.
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Название задачи не может быть пустым.");

            if (value.Trim().Length > MaxTitleLength)
                throw new ArgumentException($"Название задачи не должно быть длиннее {MaxTitleLength} символов.");

            _title = value.Trim();
        }
    }

    // Поле для описания. Защищает от null: пустое значение превращается в пустую строку.
    private string _description = string.Empty;

    /// <summary>Подробное описание задачи (необязательное). Никогда не бывает null.</summary>
    public string Description
    {
        get => _description;
        set => _description = value ?? string.Empty;
    }

    /// <summary>Срок выполнения.</summary>
    [JsonConverter(typeof(LegacyDueDateConverter))]
    public DateTime DueDate { get; set; } = DateTime.Today.AddHours(23).AddMinutes(59);

    /// <summary>Приоритет задачи.</summary>
    public Priority Priority { get; set; } = Priority.Medium;

    /// <summary>Текущий статус задачи.</summary>
    public TaskState State { get; set; } = TaskState.New;

    /// <summary>Дата и время создания задачи.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Признак просроченной задачи: срок прошёл, а задача не выполнена.
    /// Вычисляется на лету, в файл не сохраняется (JsonIgnore).
    /// </summary>
    [JsonIgnore]
    public bool IsOverdue => State != TaskState.Done && DueDate < DateTime.Now;

    /// <summary>
    /// Название вида задачи для отображения в интерфейсе.
    /// Виртуальный метод: каждый наследник возвращает своё значение (полиморфизм).
    /// </summary>
    public virtual string GetTypeName() => "Обычная";

    /// <summary>
    /// Отмечает задачу выполненной.
    /// Наследники могут переопределить поведение (см. RecurringTask).
    /// </summary>
    public virtual void MarkDone()
    {
        State = TaskState.Done;
    }

    /// <summary>
    /// Возвращает краткое текстовое описание задачи одной строкой.
    /// Наследники дополняют строку своими данными.
    /// </summary>
    public virtual string GetDetails()
    {
        // this.Priority / this.State — явное обращение к свойствам объекта
        return $"{Title} | срок: {DueDate:dd.MM.yyyy} | приоритет: {this.Priority.ToRussian()} | статус: {this.State.ToRussian()}";
    }
}
