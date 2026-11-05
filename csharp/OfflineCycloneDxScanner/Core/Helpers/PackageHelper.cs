using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OfflineCycloneDxScanner;

public static class PackageHelper
{
	public static string Purl(string ecosystem, string name, string version, IReadOnlyDictionary<string, string>? qualifiers = null, string? subpath = null)
	{
		if (!TryPurl(ecosystem, name, version, out string purl, qualifiers, subpath))
		{
			throw new ArgumentException("A PURL requires a supported type, non-empty name, and non-empty version.");
		}
		return purl;
	}

	public static bool TryPurl(string ecosystem, string name, string version, out string purl, IReadOnlyDictionary<string, string>? qualifiers = null, string? subpath = null)
	{
		purl = string.Empty;
		if (string.IsNullOrWhiteSpace(ecosystem) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(version))
		{
			return false;
		}
		string text = Normalize(ecosystem).ToLowerInvariant();
		if (1 == 0)
		{
		}
		string text2 = ((!(text == "gradle")) ? text : "maven");
		if (1 == 0)
		{
		}
		string text3 = text2;
		var (value, value2) = ParseIdentity(text3, name);
		if (string.IsNullOrEmpty(value2))
		{
			return false;
		}
		StringBuilder stringBuilder = new StringBuilder("pkg:" + EncodeType(text3) + "/");
		if (!string.IsNullOrEmpty(value))
		{
			stringBuilder.Append(EncodePath(value)).Append('/');
		}
		stringBuilder.Append(EncodeComponent(value2)).Append('@').Append(EncodeComponent(version));
		AppendQualifiers(stringBuilder, qualifiers);
		if (!string.IsNullOrWhiteSpace(subpath))
		{
			stringBuilder.Append('#').Append(EncodeSubpath(subpath));
		}
		purl = stringBuilder.ToString();
		return true;
	}

	public static string ConanPurl(string name, string version)
	{
		return Purl("conan", name, version);
	}

	public static string ComposerPurl(string name, string version)
	{
		return Purl("composer", name, version);
	}

	private static (string Namespace, string Name) ParseIdentity(string type, string value)
	{
		string text = Normalize(value);
		switch (type)
		{
		case "maven":
		{
			int num = text.IndexOf(':');
			return (num > 0 && num < text.Length - 1) ? (Namespace: text.Substring(0, num), Name: text.Substring(num + 1)) : SplitNamespace(text);
		}
		case "composer":
			return SplitNamespace(text.ToLowerInvariant());
		case "pub":
			return SplitNamespace(text.ToLowerInvariant());
		default:
			return SplitNamespace(text);
		}
	}

	private static (string Namespace, string Name) SplitNamespace(string value)
	{
		int num = value.LastIndexOf('/');
		return (num > 0 && num < value.Length - 1) ? (Namespace: value.Substring(0, num), Name: value.Substring(num + 1)) : (Namespace: string.Empty, Name: value);
	}

	private static string Normalize(string value)
	{
		return value.Normalize(NormalizationForm.FormC).Trim();
	}

	private static string EncodeType(string value)
	{
		return value.ToLowerInvariant();
	}

	private static string EncodePath(string value)
	{
		return string.Join('/', (from part in value.Split('/')
			where part.Length > 0
			select part).Select(EncodeComponent));
	}

	private static string EncodeComponent(string value)
	{
		StringBuilder stringBuilder = new StringBuilder();
		byte[] bytes = Encoding.UTF8.GetBytes(Normalize(value));
		for (int i = 0; i < bytes.Length; i++)
		{
			byte b = bytes[i];
			bool flag;
			switch (b)
			{
			case 45:
			case 46:
			case 48:
			case 49:
			case 50:
			case 51:
			case 52:
			case 53:
			case 54:
			case 55:
			case 56:
			case 57:
			case 58:
			case 65:
			case 66:
			case 67:
			case 68:
			case 69:
			case 70:
			case 71:
			case 72:
			case 73:
			case 74:
			case 75:
			case 76:
			case 77:
			case 78:
			case 79:
			case 80:
			case 81:
			case 82:
			case 83:
			case 84:
			case 85:
			case 86:
			case 87:
			case 88:
			case 89:
			case 90:
			case 95:
			case 97:
			case 98:
			case 99:
			case 100:
			case 101:
			case 102:
			case 103:
			case 104:
			case 105:
			case 106:
			case 107:
			case 108:
			case 109:
			case 110:
			case 111:
			case 112:
			case 113:
			case 114:
			case 115:
			case 116:
			case 117:
			case 118:
			case 119:
			case 120:
			case 121:
			case 122:
			case 126:
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				stringBuilder.Append((char)b);
			}
			else
			{
				stringBuilder.Append('%').Append(b.ToString("X2"));
			}
		}
		return stringBuilder.ToString();
	}

	private static void AppendQualifiers(StringBuilder builder, IReadOnlyDictionary<string, string>? qualifiers)
	{
		if (qualifiers == null || qualifiers.Count == 0)
		{
			return;
		}
		IOrderedEnumerable<KeyValuePair<string, string>> orderedEnumerable = qualifiers.Where<KeyValuePair<string, string>>((KeyValuePair<string, string> item) => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value)).OrderBy<KeyValuePair<string, string>, string>((KeyValuePair<string, string> item) => item.Key.ToLowerInvariant(), StringComparer.Ordinal);
		bool flag = true;
		foreach (KeyValuePair<string, string> item in orderedEnumerable)
		{
			builder.Append(flag ? '?' : '&');
			flag = false;
			builder.Append(EncodeQualifier(item.Key.ToLowerInvariant())).Append('=').Append(EncodeQualifier(item.Value));
		}
	}

	private static string EncodeQualifier(string value)
	{
		return EncodeComponent(value);
	}

	private static string EncodeSubpath(string value)
	{
		return string.Join('/', (from part in value.Split('/')
			where part.Length > 0
			select part).Select(EncodeComponent));
	}

	public static bool IsPlatformRequirement(string name)
	{
		string text = name.Trim().ToLowerInvariant();
		return text == "php" || text.StartsWith("ext-", StringComparison.Ordinal) || text.StartsWith("lib-", StringComparison.Ordinal) || text.StartsWith("composer-", StringComparison.Ordinal);
	}

	public static bool TryParseConanReference(string reference, out ConanReference parsed)
	{
		parsed = null;
		string[] array = reference.Split('@', 2)[0].Split('#', 2)[0].Split('/', 2);
		if (array.Length != 2 || array.Any(string.IsNullOrWhiteSpace))
		{
			return false;
		}
		string text = (reference.Contains('@') ? reference.Substring(reference.IndexOf('@') + 1) : string.Empty);
		string[] source = text.Split('#', 2)[0].Split('/', 2);
		string text2 = (reference.Contains('#') ? reference.Substring(reference.IndexOf('#') + 1) : string.Empty);
		string[] array2 = text2.Split(':', 2);
		string[] source2 = array2.ElementAtOrDefault(1)?.Split('#', 2) ?? Array.Empty<string>();
		parsed = new ConanReference(array[0], array[1], source.ElementAtOrDefault(0), source.ElementAtOrDefault(1), array2[0], source2.ElementAtOrDefault(0), source2.ElementAtOrDefault(1), reference);
		return true;
	}
}
