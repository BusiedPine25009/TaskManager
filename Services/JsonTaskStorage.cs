using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskManager.Models;

namespace TaskManager.Services;

/// <summary>
/// Слой данных: сохраняет список задач в JSON-файл и загружает его обратно.
/// Больше ничего не знает ни об интерфейсе, ни о бизнес-правилах.
/// </summary>
public class JsonTaskStorage
{
    // Полный путь к файлу с данными.
    private readonly string _filePath;

    // Настройки сериализации (общие для всех экземпляров).
    private static readonly JsonSerializerOptions Options = new()
    {
        // Красивый отступ, чтобы файл можно было читать глазами
        WriteIndented = true,
        // Русские буквы пишем как есть, а не в виде \u0414...
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // Приоритет и статус пишем словами ("High"), а не числами
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Создаёт хранилище.
    /// </summary>
    /// <param name="filePath">Путь к файлу. Если не указан, берётся путь по умолчанию.</param>
    public JsonTaskStorage(string? filePath = null)
    {
        _filePath = filePath ?? GetDefaultPath();
    }

    /// <summary>Путь к файлу с данными (для показа пользователю и в отчёте).</summary>
    public string FilePath => _filePath;
    public string BackupFilePath => _filePath + ".bak";

    /// <summary>
    /// Путь по умолчанию: папка данных пользователя / TaskManager / tasks.json.
    /// Работает на macOS, Windows и Linux.
    /// </summary>
    public static string GetDefaultPath()
    {
        string baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseFolder, "TaskManager", "tasks.json");
    }

    /// <summary>
    /// Загружает задачи из файла.
    /// Если файла ещё нет или он пустой, возвращает пустой список.
    /// Если файл повреждён, выбрасывает InvalidDataException с понятным сообщением.
    /// </summary>
    public List<TaskItem> Load()
    {
        if (!File.Exists(_filePath))
            return new List<TaskItem>();

        try
        {
            string json = File.ReadAllText(_filePath);

            if (string.IsNullOrWhiteSpace(json))
                return new List<TaskItem>();

            return JsonSerializer.Deserialize<List<TaskItem>>(json, Options) ?? new List<TaskItem>();
        }
        catch (JsonException ex)
        {
            throw CorruptFileException(ex);
        }
        catch (NotSupportedException ex)
        {
            throw CorruptFileException(ex);
        }
        catch (ArgumentException ex)
        {
            throw CorruptFileException(ex);
        }
    }

    /// <summary>
    /// Сохраняет список задач в файл.
    /// Сначала пишет во временный файл, потом заменяет им основной:
    /// так данные не потеряются, если программа упадёт посреди записи.
    /// </summary>
    /// <param name="tasks">Задачи для сохранения.</param>
    public void Save(IEnumerable<TaskItem> tasks)
    {
        // Создаём папку, если её ещё нет
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(tasks.ToList(), Options);

        string tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        if (File.Exists(_filePath))
            File.Copy(_filePath, BackupFilePath, overwrite: true);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    /// <summary>Reads the backup only after the user explicitly chooses recovery.</summary>
    public List<TaskItem> LoadBackup()
    {
        if (!File.Exists(BackupFilePath))
            throw new FileNotFoundException(Loc.T("err.backup.notFound"), BackupFilePath);

        string json = File.ReadAllText(BackupFilePath);
        return JsonSerializer.Deserialize<List<TaskItem>>(json, Options) ?? new List<TaskItem>();
    }

    private InvalidDataException CorruptFileException(Exception inner)
    {
        string copyPath = _filePath + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
        try
        {
            File.Copy(_filePath, copyPath, overwrite: false);
        }
        catch (IOException)
        {
            copyPath = Loc.T("err.data.copyFailed");
        }

        string backupHint = File.Exists(BackupFilePath)
            ? Loc.T("err.data.backupHint", BackupFilePath)
            : string.Empty;
        return new InvalidDataException(
            Loc.T("err.data.corrupted", copyPath, backupHint), inner);
    }
}
