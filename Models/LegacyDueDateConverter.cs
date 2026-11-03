using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskManager.Models;

/// <summary>
/// Reads legacy date-only values as the end of that local day.
/// New values continue to use the normal ISO DateTime representation.
/// </summary>
public sealed class LegacyDueDateConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string value = reader.GetString() ?? throw new JsonException("Срок выполнения не указан.");
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime dateOnly))
            return dateOnly.Date.AddHours(23).AddMinutes(59);

        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            .ToLocalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
}
