using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OfflineCycloneDxScanner;

public static class LicenseExpressionParser
{
	public static IReadOnlyCollection<string> Parse(IEnumerable<string> expressions)
	{
		return (IReadOnlyCollection<string>)(object)(from value in expressions.SelectMany(Parse)
			where value.Length > 0
			select value).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	public static IReadOnlyCollection<string> Parse(string expression)
	{
		if (string.IsNullOrWhiteSpace(expression))
		{
			return (IReadOnlyCollection<string>)(object)Array.Empty<string>();
		}
		return (IReadOnlyCollection<string>)(object)(from value in Regex.Split(expression, "(?i)\\s+(?:and|or)\\s+|[,/|;]").Select(Normalize)
			where value.Length > 0
			select value).ToArray();
	}

	private static string Normalize(string value)
	{
		string text = value.Trim();
		return text.Equals("GPL-2.0+", StringComparison.OrdinalIgnoreCase) ? "GPL-2.0-or-later" : text;
	}
}
