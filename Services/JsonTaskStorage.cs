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
            // Файл не является корректным JSON
            throw new InvalidDataException("Файл данных повреждён и не может быть прочитан.", ex);
        }
        catch (NotSupportedException ex)
        {
            // В файле указан неизвестный вид задачи (например, файл от другой версии программы)
            throw new InvalidDataException("Файл данных содержит неизвестный вид задачи.", ex);
        }
        catch (ArgumentException ex)
        {
            // В файле есть значения, не прошедшие проверку в свойствах (например, пустое название)
            throw new InvalidDataException($"Файл данных содержит некорректные значения: {ex.Message}", ex);
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
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
