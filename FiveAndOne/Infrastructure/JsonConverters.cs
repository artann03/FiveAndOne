using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FiveAndOne.API.Infrastructure;

/// <summary>TimeOnly as "HH:mm" (also accepts "HH:mm:ss"), e.g. opening hours "08:00".</summary>
public sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] Formats = { "HH:mm", "HH:mm:ss", "H:mm", "HH:mm:ss.FFFFFFF" };

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is not null && TimeOnly.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time;
        }

        throw new JsonException("Time must be in HH:mm format, e.g. \"18:30\".");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("HH:mm", CultureInfo.InvariantCulture));
}
