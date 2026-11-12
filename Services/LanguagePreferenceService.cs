using System;
using System.Globalization;
using System.IO;

namespace TaskManager.Services;

/// <summary>
/// Загружает и сохраняет выбранный язык отдельно от пользовательских задач.
/// Файл language.txt располагается рядом с настройкой темы, в папке TaskManager
/// внутри LocalApplicationData.
/// </summary>
public sealed class LanguagePreferenceService
{
    private readonly string _filePath;

    /// <summary>
    /// Создаёт сервис языка. Необязательный путь позволяет изолированно проверять
    /// чтение и сохранение настройки без обращения к папке данных пользователя.
    /// </summary>
    /// <param name="filePath">Путь для файла языка или null для стандартного пути.</param>
    public LanguagePreferenceService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskManager", "language.txt");
    }

    /// <summary>
    /// Возвращает сохранённый ru/en/kk, если он допустим. При отсутствии,
    /// повреждении файла или неизвестном значении выбирает подходящий язык системы,
    /// а для всех остальных системных языков — русский.
    /// </summary>
    public string Load()
    {
        string fallback = GetSystemLanguage();
        try
        {
            if (!File.Exists(_filePath))
                return fallback;

            string saved = File.ReadAllText(_filePath).Trim().ToLowerInvariant();
            return IsSupported(saved) ? saved : fallback;
        }
        catch (IOException)
        {
            return fallback;
        }
        catch (UnauthorizedAccessException)
        {
            return fallback;
        }
    }

    /// <summary>Сохраняет выбранный язык, создавая при необходимости папку настроек.</summary>
    /// <param name="language">Допустимый код языка ru, en или kk.</param>
    public void Save(string language)
    {
        string normalized = language.Trim().ToLowerInvariant();
        if (!IsSupported(normalized))
            throw new ArgumentOutOfRangeException(nameof(language));

        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(_filePath, normalized);
    }

    /// <summary>Определяет системный ru/en/kk или выбирает русский по умолчанию.</summary>
    private static string GetSystemLanguage()
    {
        string systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return IsSupported(systemLanguage) ? systemLanguage : "ru";
    }

    /// <summary>Проверяет принадлежность к трём языкам, поддерживаемым интерфейсом.</summary>
    /// <param name="language">Код для проверки.</param>
    private static bool IsSupported(string language) => language is "ru" or "en" or "kk";
}
