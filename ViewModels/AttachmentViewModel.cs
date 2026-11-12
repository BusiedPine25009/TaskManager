using System;
using System.Globalization;
using TaskManager.Models;

namespace TaskManager.ViewModels;

/// <summary>Компактное представление вложения для окна редактирования задачи.</summary>
public sealed class AttachmentViewModel
{
    /// <summary>Создаёт отображаемую модель для метаданных вложения.</summary>
    public AttachmentViewModel(TaskAttachment attachment) => Attachment = attachment;

    /// <summary>Исходные метаданные вложения.</summary>
    public TaskAttachment Attachment { get; }

    /// <summary>Имя файла, введённое пользователем или полученное из выбранного файла.</summary>
    public string FileName => Attachment.FileName;

    /// <summary>Расширение, показываемое отдельно для быстрого поиска по списку.</summary>
    public string Extension => string.IsNullOrEmpty(Attachment.Extension) ? "—" : Attachment.Extension.ToUpperInvariant();

    /// <summary>Размер, отформатированный без изменения фактического значения в JSON.</summary>
    public string SizeText => FormatSize(Attachment.Size);

    /// <summary>Переводит размер из байтов в короткий удобочитаемый вид.</summary>
    private static string FormatSize(long size)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = Math.Max(0, size);
        int index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }
        return $"{value.ToString(index == 0 ? "0" : "0.0", CultureInfo.CurrentCulture)} {units[index]}";
    }
}
