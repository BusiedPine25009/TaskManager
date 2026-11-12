using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskManager.Models;

namespace TaskManager.Services;

public sealed class RecycleBinStorage
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public RecycleBinStorage(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskManager", "recycle-bin.json");
    }

    public string FilePath => _filePath;
    public string BackupFilePath => _filePath + ".bak";

    public List<DeletedTask> Load()
    {
        if (!File.Exists(_filePath))
            return new();

        try
        {
            string json = File.ReadAllText(_filePath);
            return string.IsNullOrWhiteSpace(json)
                ? new()
                : JsonSerializer.Deserialize<List<DeletedTask>>(json, Options) ?? new();
        }
        catch (JsonException ex)
        {
            throw CreateCorruptException(ex);
        }
        catch (NotSupportedException ex)
        {
            throw CreateCorruptException(ex);
        }
    }

    public void Save(IEnumerable<DeletedTask> entries)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(entries.ToList(), Options);
        string tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        if (File.Exists(_filePath))
            File.Copy(_filePath, BackupFilePath, overwrite: true);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private InvalidDataException CreateCorruptException(Exception inner)
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

        return new InvalidDataException(
            Loc.T("err.bin.corrupted", copyPath), inner);
    }
}

public sealed class DeletedTask
{
    public TaskItem Task { get; set; } = new();
    public DateTime DeletedAt { get; set; } = DateTime.Now;
}
