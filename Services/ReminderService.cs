using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaskManager.Models;

namespace TaskManager.Services;

/// <summary>
/// Ищет наступившие напоминания и отправляет системное уведомление macOS,
/// пока приложение работает или свёрнуто. Доставка после полного закрытия
/// приложения требует отдельного фонового агента и намеренно не имитируется.
/// </summary>
public sealed class ReminderService
{
    /// <summary>
    /// Отправляет все ещё не отправленные напоминания, срок которых наступил.
    /// Метка отправки сохраняется сразу, поэтому повторный запуск не дублирует уведомление.
    /// </summary>
    /// <param name="tasks">Сервис, управляющий сохранёнными задачами.</param>
    /// <returns>Задачи, для которых была сделана попытка отправки.</returns>
    public IReadOnlyList<TaskItem> CheckDue(TaskService tasks)
    {
        List<TaskItem> sent = new();
        DateTime now = DateTime.Now;
        foreach (TaskItem task in tasks.GetAll())
        {
            if (task.State == TaskState.Done || !task.ReminderAt.HasValue ||
                task.ReminderAt.Value > now || task.ReminderSentAt.HasValue)
                continue;

            ShowMacNotification(task.Title);
            tasks.MarkReminderSent(task.Id, now);
            sent.Add(task);
        }
        return sent;
    }

    /// <summary>Вызывает стандартное уведомление macOS, безопасно экранируя название задачи.</summary>
    private static void ShowMacNotification(string taskTitle)
    {
        string escaped = taskTitle.Replace("\\", "\\\\").Replace("\"", "\\\"");
        ProcessStartInfo startInfo = new("osascript") { UseShellExecute = false };
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add($"display notification \"{escaped}\" with title \"TaskManager\"");
        try { Process.Start(startInfo); }
        catch (Exception) { /* Метка отправки защищает от повторных всплывающих уведомлений. */ }
    }
}
