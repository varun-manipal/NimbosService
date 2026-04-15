using System.Text.Json;
using System.Text.Json.Serialization;

namespace NimbosService.Utilities;

/// <summary>
/// Forces all DateTime values to be serialised as UTC with a trailing 'Z'.
/// EF Core reads DateTime columns from SQL Server with DateTimeKind.Unspecified,
/// which causes System.Text.Json to omit the timezone designator. This converter
/// ensures clients always receive a well-formed ISO-8601 string.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString()!;
        var dt = DateTime.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind);
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc)
            .ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"));
    }
}
