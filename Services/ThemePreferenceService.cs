using System;
using System.IO;

namespace TaskManager.Services;

public sealed class ThemePreferenceService
{
    private readonly string _filePath;

    public ThemePreferenceService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskManager", "theme.txt");
    }

    public string Load() => File.Exists(_filePath) ? File.ReadAllText(_filePath).Trim() : "Default";

    public void Save(string value)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(_filePath, value);
    }
}
