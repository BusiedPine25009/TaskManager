using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Avalonia.Platform;

namespace TaskManager.Services;

/// <summary>
/// Хранит переводы интерфейса и предоставляет их XAML и C#-коду.
/// Словари загружаются из встроенных JSON-ресурсов, а при смене языка обновляются
/// индексаторные привязки и культура форматирования текущих и новых потоков.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    private readonly Dictionary<string, Dictionary<string, string>> _translations;
    private string _language = "ru";

    /// <summary>Единственный экземпляр локализатора для всего приложения.</summary>
    public static Loc Instance { get; } = new();

    private Loc()
    {
        _translations = new Dictionary<string, Dictionary<string, string>>
        {
            ["ru"] = LoadDictionary("ru"),
            ["en"] = LoadDictionary("en"),
            ["kk"] = LoadDictionary("kk")
        };
        ApplyCulture(_language);
    }

    /// <summary>Событие уведомляет окна о смене языка интерфейса.</summary>
    public event EventHandler? LanguageChanged;

    /// <summary>Событие обновляет XAML-привязки и свойства языка локализатора.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Двухбуквенный код активного языка: ru, en или kk.</summary>
    public string Language => _language;

    /// <summary>Признак активного русского языка, используемый отметкой в меню.</summary>
    public bool IsRussian => _language == "ru";

    /// <summary>Признак активного английского языка, используемый отметкой в меню.</summary>
    public bool IsEnglish => _language == "en";

    /// <summary>Признак активного казахского языка, используемый отметкой в меню.</summary>
    public bool IsKazakh => _language == "kk";

    /// <summary>
    /// Возвращает перевод по ключу. Если ключ отсутствует в активном словаре,
    /// сначала ищется русский перевод, а затем возвращается сам ключ.
    /// </summary>
    public string this[string key] => GetTranslation(key);

    /// <summary>
    /// Активирует ru, en или kk, применяет соответствующую культуру и уведомляет
    /// открытые окна. Неизвестное значение безопасно заменяется русским языком.
    /// </summary>
    /// <param name="language">Код языка, который нужно включить.</param>
    public void SetLanguage(string language)
    {
        string normalized = NormalizeLanguage(language);
        if (_language == normalized)
        {
            ApplyCulture(normalized);
        }

        else
        {
            _language = normalized;
            ApplyCulture(normalized);
        }

        NotifyLanguageChanged();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Возвращает непараметризованную строку для использования в C#.</summary>
    /// <param name="key">Ключ перевода из JSON-файлов локализации.</param>
    public static string T(string key) => Instance.GetTranslation(key);

    /// <summary>
    /// Возвращает перевод с форматированием аргументов в активной культуре.
    /// Числа параметров должны соответствовать плейсхолдерам в JSON-словаре.
    /// </summary>
    /// <param name="key">Ключ строки с плейсхолдерами {0}, {1} и далее.</param>
    /// <param name="args">Значения, подставляемые в строку.</param>
    public static string T(string key, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, Instance.GetTranslation(key), args);

    /// <summary>
    /// Выбирает грамматическую форму слова из перевода, разделённого символом |.
    /// Для русского применяются три формы, для английского — единственное/множественное,
    /// для казахского используется единственная неизменяемая форма.
    /// </summary>
    /// <param name="key">Ключ слова с формами множественного числа.</param>
    /// <param name="number">Число, к которому относится слово.</param>
    public static string Plural(string key, int number)
    {
        string[] forms = T(key).Split('|');
        if (forms.Length == 1)
            return forms[0];

        int index;
        if (Instance.Language == "kk")
        {
            index = 0;
        }
        else if (Instance.Language == "en")
        {
            index = number == 1 ? 0 : 1;
        }
        else
        {
            int absolute = (int)(Math.Abs((long)number) % 100);
            int lastDigit = absolute % 10;
            index = absolute is >= 11 and <= 14 ? 2
                : lastDigit == 1 ? 0
                : lastDigit is >= 2 and <= 4 ? 1
                : 2;
        }

        return forms[Math.Min(index, forms.Length - 1)];
    }

    /// <summary>Читает плоский JSON-словарь из собранных Avalonia-ресурсов.</summary>
    /// <param name="language">Код файла локализации без расширения.</param>
    private static Dictionary<string, string> LoadDictionary(string language)
    {
        Uri resourceUri = new($"avares://TaskManager/Assets/Localization/{language}.json");
        using Stream stream = AssetLoader.Open(resourceUri);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
               ?? new Dictionary<string, string>();
    }

    /// <summary>Ищет строку сначала в активном языке, затем в русском словаре.</summary>
    /// <param name="key">Ключ поиска.</param>
    private string GetTranslation(string key)
    {
        if (_translations.TryGetValue(_language, out Dictionary<string, string>? current) &&
            current.TryGetValue(key, out string? translation))
            return translation;

        if (_translations.TryGetValue("ru", out Dictionary<string, string>? russian) &&
            russian.TryGetValue(key, out translation))
            return translation;

        return key;
    }

    /// <summary>
    /// Уведомляет Avalonia обо всех вариантах изменения индексатора. Разные части
    /// механизма привязок используют имена <c>Item</c> и <c>Item[]</c>; пустое имя
    /// дополнительно заставляет пересчитать свойства, зависящие от текущего языка.
    /// </summary>
    private void NotifyLanguageChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRussian)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnglish)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsKazakh)));
    }

    /// <summary>Проверяет код языка и приводит неподдерживаемые значения к ru.</summary>
    /// <param name="language">Исходный код языка.</param>
    private static string NormalizeLanguage(string? language)
    {
        string normalized = language?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized is "ru" or "en" or "kk" ? normalized : "ru";
    }

    /// <summary>Назначает культуру форматирования дат и значений чисел.</summary>
    /// <param name="language">Код языка, для которого выбирается культура.</param>
    private static void ApplyCulture(string language)
    {
        string cultureName = language switch
        {
            "en" => "en-US",
            "kk" => "kk-KZ",
            _ => "ru-RU"
        };
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
