using System;
using System.Collections.Generic;

namespace OfflineCycloneDxScanner;

public static class XmlHelper
{
	public static string? ResolveProperty(string? value, IReadOnlyDictionary<string, string> properties)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		if (value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}'))
		{
			return properties.GetValueOrDefault(value.Substring(2, value.Length - 1 - 2));
		}
		return value;
	}
}
