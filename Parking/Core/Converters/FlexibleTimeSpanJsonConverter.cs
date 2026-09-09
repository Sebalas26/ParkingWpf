using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Parking.Core.Converters;

public class FlexibleTimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return TimeSpan.Zero;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out var ticks))
            {
                return TimeSpan.FromTicks(ticks);
            }
            if (reader.TryGetDouble(out var totalSeconds))
            {
                return TimeSpan.FromSeconds(totalSeconds);
            }
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str))
            {
                return TimeSpan.Zero;
            }

            str = str.Trim();

            // 1. Intentar formato estándar TimeSpan.TryParse
            if (TimeSpan.TryParse(str, CultureInfo.InvariantCulture, out var ts))
            {
                return ts;
            }

            // 2. Intentar formatos comunes "hh:mm", "h:mm", "hh:mm:ss", "h:m:s", etc.
            string[] formats = { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss", @"h\:mm\:ss", @"d\.hh\:mm\:ss", @"d\.h\:m\:s" };
            if (TimeSpan.TryParseExact(str, formats, CultureInfo.InvariantCulture, TimeSpanStyles.None, out ts))
            {
                return ts;
            }

            // 3. Si viene como hora completa parseable por DateTime
            if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt.TimeOfDay;
            }
        }

        return TimeSpan.Zero;
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture));
    }
}

public class NullableFlexibleTimeSpanJsonConverter : JsonConverter<TimeSpan?>
{
    public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out var ticks))
            {
                return TimeSpan.FromTicks(ticks);
            }
            if (reader.TryGetDouble(out var totalSeconds))
            {
                return TimeSpan.FromSeconds(totalSeconds);
            }
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str))
            {
                return null;
            }

            str = str.Trim();

            // 1. Intentar formato estándar TimeSpan.TryParse
            if (TimeSpan.TryParse(str, CultureInfo.InvariantCulture, out var ts))
            {
                return ts;
            }

            // 2. Intentar formatos comunes "hh:mm", "h:mm", "hh:mm:ss", "h:m:s", etc.
            string[] formats = { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss", @"h\:mm\:ss", @"d\.hh\:mm\:ss", @"d\.h\:m\:s" };
            if (TimeSpan.TryParseExact(str, formats, CultureInfo.InvariantCulture, TimeSpanStyles.None, out ts))
            {
                return ts;
            }

            // 3. Si viene como hora completa parseable por DateTime
            if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt.TimeOfDay;
            }
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
