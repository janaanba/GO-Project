using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OfflineCycloneDxScanner;

public sealed class ComposerAnalyzer : FileProjectAnalyzer
{
	public ComposerAnalyzer(IFileSystem fileSystem)
		: base("Composer", "composer.lock", "composer", fileSystem)
	{
	}

	public override void Analyze(string root, ScanOptions options, DependencyGraph graph, IDiagnostics diagnostics)
	{
		foreach (string item in base.FileSystem.FindFiles(root, base.FileName))
		{
			if (IsVendorLockfile(item))
			{
				continue;
			}
			string content = base.FileSystem.ReadAllText(item);
			using JsonDocument jsonDocument = JsonHelper.ReadDocument(content);
			HashSet<string> directDependencies = ReadDirectDependencies(item);
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			int searchOffset = 0;
			string[] array = new string[2] { "packages", "packages-dev" };
			foreach (string text in array)
			{
				if (!jsonDocument.RootElement.TryGetProperty(text, out var value))
				{
					continue;
				}
				foreach (JsonElement item2 in value.EnumerateArray())
				{
					string text2 = JsonHelper.StringProperty(item2, "name") ?? string.Empty;
					string text3 = JsonHelper.StringProperty(item2, "version") ?? string.Empty;
					if (text2.Length != 0 && text3.Length != 0 && !dictionary.ContainsKey(text2))
					{
						string purl = PackageHelper.ComposerPurl(text2, text3);
						int? num = JsonHelper.FindComposerPackageLine(content, text2, ref searchOffset);
						string? relationship = directDependencies.Contains(text2) ? "direct" : "indirect";
						string value2 = graph.Add(new DependencyComponent(text2, text3, "library", purl, base.Ecosystem, text == "packages-dev", ReadLicenses(item2), item, null, num, num, null, relationship));
						dictionary[text2] = value2;
					}
				}
			}
			AddEdges(jsonDocument, dictionary, graph);
		}
	}

	private HashSet<string> ReadDirectDependencies(string lockPath)
	{
		string jsonPath = Path.Combine(Path.GetDirectoryName(lockPath) ?? string.Empty, "composer.json");
		HashSet<string> dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (!base.FileSystem.FileExists(jsonPath))
		{
			return dependencies;
		}

		using JsonDocument document = JsonHelper.ReadDocument(base.FileSystem.ReadAllText(jsonPath));
		foreach (string propertyName in new[] { "require", "require-dev" })
		{
			if (!document.RootElement.TryGetProperty(propertyName, out JsonElement values) || values.ValueKind != JsonValueKind.Object)
			{
				continue;
			}
			foreach (JsonProperty property in values.EnumerateObject())
			{
				if (!PackageHelper.IsPlatformRequirement(property.Name))
				{
					dependencies.Add(property.Name);
				}
			}
		}
		return dependencies;
	}

	private static bool IsVendorLockfile(string path)
	{
		return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
			.Any(part => string.Equals(part, "vendor", StringComparison.OrdinalIgnoreCase));
	}

	private static void AddEdges(JsonDocument document, IReadOnlyDictionary<string, string> packages, DependencyGraph graph)
	{
		string[] array = new string[2] { "packages", "packages-dev" };
		foreach (string propertyName in array)
		{
			if (!document.RootElement.TryGetProperty(propertyName, out var value))
			{
				continue;
			}
			foreach (JsonElement item in value.EnumerateArray())
			{
				string key = JsonHelper.StringProperty(item, "name") ?? string.Empty;
				if (!packages.TryGetValue(key, out string value2) || !item.TryGetProperty("require", out var value3))
				{
					continue;
				}
				foreach (JsonProperty item2 in from property in value3.EnumerateObject()
					where !PackageHelper.IsPlatformRequirement(property.Name)
					select property)
				{
					if (packages.TryGetValue(item2.Name, out string value4))
					{
						graph.AddInstanceEdge(value2, value4);
					}
				}
			}
		}
	}

	private static IReadOnlyCollection<string> ReadLicenses(JsonElement entry)
	{
		if (!entry.TryGetProperty("license", out var value))
		{
			return (IReadOnlyCollection<string>)(object)Array.Empty<string>();
		}
		if (value.ValueKind == JsonValueKind.Array)
		{
			return LicenseExpressionParser.Parse(from jsonElement in value.EnumerateArray()
				where jsonElement.ValueKind == JsonValueKind.String
				select jsonElement.GetString() ?? string.Empty);
		}
		IReadOnlyCollection<string> result;
		if (value.ValueKind != JsonValueKind.String)
		{
			IReadOnlyCollection<string> readOnlyCollection = (IReadOnlyCollection<string>)(object)Array.Empty<string>();
			result = readOnlyCollection;
		}
		else
		{
			result = LicenseExpressionParser.Parse(value.GetString() ?? string.Empty);
		}
		return result;
	}
}
