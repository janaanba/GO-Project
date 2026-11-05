using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OfflineCycloneDxScanner;

public static class JsonHelper
{
	public static JsonDocument ReadDocument(string content)
	{
		return JsonDocument.Parse(content);
	}

	public static string? StringProperty(JsonElement element, string propertyName)
	{
		JsonElement value;
		return element.TryGetProperty(propertyName, out value) ? value.GetString() : null;
	}

	public static int? FindValueLine(string content, string value, ref int searchOffset)
	{
		string text = "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
		int num = content.IndexOf(text, searchOffset, StringComparison.Ordinal);
		if (num < 0)
		{
			return null;
		}
		searchOffset = num + text.Length;
		return content.Substring(0, num).Count((char character) => character == '\n') + 1;
	}

	public static int? FindComposerPackageLine(string content, string name, ref int searchOffset)
	{
		string text = Regex.Escape(name);
		string pattern = "\\\"name\\\"\\s*:\\s*\\\"" + text + "\\\"\\s*,\\s*\\\"version\\\"";
		Match match = Regex.Match(content.Substring(searchOffset), pattern, RegexOptions.CultureInvariant);
		if (!match.Success)
		{
			return null;
		}
		int num = searchOffset + match.Index;
		searchOffset = num + match.Length;
		return content.Substring(0, num).Count((char character) => character == '\n') + 1;
	}
}
