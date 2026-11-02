using System;

namespace TaskManager.Models;

/// <summary>
/// Повторяющаяся задача (например, «оплатить интернет» каждые 30 дней).
/// После выполнения не закрывается, а переносится на следующий срок.
/// </summary>
public class RecurringTask : TaskItem
{
    // Интервал повторения в днях (хранится в закрытом поле, меняется через свойство).
    private int _repeatEveryDays = 7;

    /// <summary>
    /// Через сколько дней задача повторяется. Допустимо от 1 до 365.
    /// </summary>
    public int RepeatEveryDays
    {
        get => _repeatEveryDays;
        set
        {
            if (value < 1 || value > 365)
                throw new ArgumentException("Интервал повторения должен быть от 1 до 365 дней.");

            _repeatEveryDays = value;
        }
    }

    /// <summary>Название вида задачи (переопределение базового метода).</summary>
    public override string GetTypeName() => "Повторяющаяся";

    /// <summary>
    /// Вместо закрытия задачи переносит срок вперёд на RepeatEveryDays дней
    /// и возвращает статус «Новая». Срок сдвигается до тех пор,
    /// пока не окажется в будущем (на случай, если задача сильно просрочена).
    /// </summary>
    public override void MarkDone()
    {
        do
        {
            DueDate = DueDate.AddDays(RepeatEveryDays);
        }
        while (DueDate.Date < DateTime.Today);

        State = TaskState.New;
    }

    /// <summary>Дополняет базовое описание информацией о повторе.</summary>
    public override string GetDetails()
    {
        return $"{base.GetDetails()} | повтор: каждые {RepeatEveryDays} дн.";
    }
}
