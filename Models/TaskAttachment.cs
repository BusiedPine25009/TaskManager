using System;

namespace TaskManager.Models;

/// <summary>
/// Метаданные файла, скопированного во внутреннее хранилище приложения.
/// Содержимое файла в JSON не записывается: там остаётся только относительный путь.
/// </summary>
public sealed class TaskAttachment
{
    /// <summary>Уникальный идентификатор вложения.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Идентификатор задачи, которой принадлежит вложение.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Исходное имя файла, отображаемое пользователю.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Расширение файла с точкой или пустая строка.</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>Размер файла в байтах на момент прикрепления.</summary>
    public long Size { get; set; }

    /// <summary>Безопасный относительный путь внутри папки вложений.</summary>
    public string RelativePath { get; set; } = string.Empty;
}
