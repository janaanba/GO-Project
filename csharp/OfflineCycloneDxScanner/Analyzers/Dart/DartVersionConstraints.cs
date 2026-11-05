using System;
using System.Linq;

namespace OfflineCycloneDxScanner;

internal static class DartVersionConstraints
{
	public static string? MinimumVersion(string constraint)
	{
		string text = constraint.TrimStart();
		int num = (text.StartsWith(">=", StringComparison.Ordinal) ? 2 : (text.StartsWith("^", StringComparison.Ordinal) ? 1 : 0));
		if (num == 0)
		{
			return null;
		}
		string text2 = text.Substring(num).Split(new char[3] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
		return (text2 == null) ? null : Normalize(text2);
	}

	private static string? Normalize(string value)
	{
		string[] array = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
		if (array.Length < 2 || array.Any((string part) => !part.All(char.IsDigit)))
		{
			return null;
		}
		return string.Join('.', array.Take(3).Concat(Enumerable.Repeat("0", Math.Max(0, 3 - array.Length))));
	}
}
