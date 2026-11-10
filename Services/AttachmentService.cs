using System;
using System.Diagnostics;
using System.IO;
using TaskManager.Models;

namespace TaskManager.Services;

/// <summary>
/// Копирует вложения в папку данных TaskManager и безопасно открывает или удаляет их.
/// Оригинальные файлы пользователя сервис никогда не меняет и не удаляет.
/// </summary>
public sealed class AttachmentService
{
    private readonly string _rootPath;

    /// <summary>Создаёт сервис; путь можно подменить в автоматических проверках.</summary>
    /// <param name="rootPath">Корневая папка внутреннего хранилища вложений.</param>
    public AttachmentService(string? rootPath = null)
    {
        _rootPath = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskManager", "attachments");
    }

    /// <summary>Папка, в которой TaskManager хранит независимые копии файлов.</summary>
    public string RootPath => _rootPath;

    /// <summary>
    /// Проверяет файл, создаёт уникальное имя и копирует его в папку задачи.
    /// </summary>
    /// <param name="taskId">Идентификатор задачи-владельца.</param>
    /// <param name="sourcePath">Путь к выбранному пользователем исходному файлу.</param>
    /// <returns>Метаданные созданного вложения.</returns>
    public TaskAttachment Add(Guid taskId, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException(Loc.T("attachment.err.notFound"));

        FileInfo source = new(sourcePath);
        using (source.Open(FileMode.Open, FileAccess.Read, FileShare.Read)) { }

        string extension = Path.GetExtension(source.Name);
        Guid attachmentId = Guid.NewGuid();
        string folderName = taskId.ToString("N");
        string storedName = attachmentId.ToString("N") + extension;
        string relativePath = Path.Combine(folderName, storedName);
        string destination = GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source.FullName, destination, overwrite: false);

        return new TaskAttachment
        {
            Id = attachmentId,
            TaskId = taskId,
            FileName = source.Name,
            Extension = extension,
            Size = source.Length,
            RelativePath = relativePath
        };
    }

    /// <summary>Проверяет наличие сохранённой копии вложения.</summary>
    public bool Exists(TaskAttachment attachment) => File.Exists(GetFullPath(attachment.RelativePath));

    /// <summary>
    /// Открывает копию вложения системным приложением macOS через команду <c>open</c>.
    /// </summary>
    /// <param name="attachment">Вложение, которое нужно открыть.</param>
    public void Open(TaskAttachment attachment)
    {
        string path = GetFullPath(attachment.RelativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException(Loc.T("attachment.err.missing"), path);

        ProcessStartInfo startInfo = new("open") { UseShellExecute = false };
        startInfo.ArgumentList.Add(path);
        Process? process = Process.Start(startInfo);
        if (process is null)
            throw new InvalidOperationException(Loc.T("attachment.err.open"));
    }

    /// <summary>Удаляет только внутреннюю копию прикреплённого файла.</summary>
    public void Delete(TaskAttachment attachment)
    {
        string path = GetFullPath(attachment.RelativePath);
        if (File.Exists(path))
            File.Delete(path);
        DeleteEmptyParentDirectory(path);
    }

    /// <summary>Удаляет внутренние копии всех вложений окончательно удалённой задачи.</summary>
    public void DeleteAll(TaskItem task)
    {
        foreach (TaskAttachment attachment in task.Attachments)
            Delete(attachment);
    }

    /// <summary>
    /// Формирует путь только внутри хранилища. Внешний или содержащий переходы
    /// относительный путь отклоняется, чтобы JSON не мог обратиться к чужому файлу.
    /// </summary>
    private string GetFullPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new InvalidDataException(Loc.T("attachment.err.path"));

        string root = Path.GetFullPath(_rootPath) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        if (!path.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidDataException(Loc.T("attachment.err.path"));
        return path;
    }

    /// <summary>Убирает пустую папку задачи после удаления последнего вложения.</summary>
    private static void DeleteEmptyParentDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory) &&
            Directory.GetFileSystemEntries(directory).Length == 0)
            Directory.Delete(directory);
    }
}
