using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OfflineCycloneDxScanner;

internal sealed class CycloneTimestampConverter : JsonConverter<DateTimeOffset>
{
	public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return DateTimeOffset.Parse(reader.GetString() ?? string.Empty, CultureInfo.InvariantCulture);
	}

	public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss+00:00", CultureInfo.InvariantCulture));
	}
}
