using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OfflineCycloneDxScanner;

public sealed class ConanAnalyzer : FileProjectAnalyzer
{
	public ConanAnalyzer(IFileSystem fileSystem)
		: base("Conan", "conan.lock", "conan", fileSystem)
	{
	}

	public override void Analyze(string root, ScanOptions options, DependencyGraph graph, IDiagnostics diagnostics)
	{
		foreach (string item in base.FileSystem.FindFiles(root, base.FileName))
		{
			string content = base.FileSystem.ReadAllText(item);
			using JsonDocument jsonDocument = JsonHelper.ReadDocument(content);
			Dictionary<string, string> licenses = ReadCachedLicenses();
			JsonElement value3;
			if (jsonDocument.RootElement.TryGetProperty("graph_lock", out var value) && value.TryGetProperty("nodes", out var value2))
			{
				AnalyzeGraph(value2, item, content, licenses, graph);
			}
			else if (jsonDocument.RootElement.TryGetProperty("requires", out value3))
			{
				AnalyzeRequirements(value3, item, content, licenses, graph);
			}
		}
	}

	private Dictionary<string, string> ReadCachedLicenses()
	{
		List<string> cacheRoots = new List<string>();
		string? conanHome = Environment.GetEnvironmentVariable("CONAN_HOME");
		string? conanUserHome = Environment.GetEnvironmentVariable("CONAN_USER_HOME");
		if (!string.IsNullOrWhiteSpace(conanHome))
		{
			cacheRoots.Add(Path.Combine(conanHome, "p"));
		}
		if (!string.IsNullOrWhiteSpace(conanUserHome))
		{
			cacheRoots.Add(Path.Combine(conanUserHome, ".conan", "data"));
		}
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		cacheRoots.Add(Path.Combine(home, ".conan2", "p"));
		cacheRoots.Add(Path.Combine(home, ".conan", "data"));

		Dictionary<string, string> licenses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (string cacheRoot in cacheRoots.Where(base.FileSystem.DirectoryExists).Distinct(StringComparer.OrdinalIgnoreCase))
		{
			foreach (string conanFile in base.FileSystem.FindFiles(cacheRoot, "conanfile.py"))
			{
				string? name = null;
				string? license = null;
				foreach (string line in base.FileSystem.ReadLines(conanFile))
				{
					name ??= ReadAttribute(line, "name");
					license ??= ReadAttribute(line, "license");
				}
				if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(license))
				{
					licenses[name] = license;
				}
			}
		}
		return licenses;
	}

	private static string? ReadAttribute(string line, string attribute)
	{
		string[] parts = line.Split('=', 2);
		return parts.Length == 2 && string.Equals(parts[0].Trim(), attribute, StringComparison.Ordinal)
			? parts[1].Trim().Trim('"', '\'')
			: null;
	}

	private static void AnalyzeGraph(JsonElement nodes, string path, string content, IReadOnlyDictionary<string, string> licenses, DependencyGraph graph)
	{
		JsonElement value;
		JsonElement value2;
		HashSet<string> hashSet = ((nodes.TryGetProperty("0", out value) && value.TryGetProperty("requires", out value2)) ? (from jsonElement in value2.EnumerateArray()
			select jsonElement.GetString()).OfType<string>().ToHashSet<string>(StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal));
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		int searchOffset = 0;
		foreach (JsonProperty item in nodes.EnumerateObject())
		{
			string text = JsonHelper.StringProperty(item.Value, "ref");
			if (!string.IsNullOrEmpty(text) && PackageHelper.TryParseConanReference(text, out ConanReference parsed))
			{
				int? num = JsonHelper.FindValueLine(content, text, ref searchOffset);
				string purl = PackageHelper.ConanPurl(parsed.Name, parsed.Version);
				string relationship = (hashSet.Contains(item.Name) ? "direct" : "indirect");
				string value3 = graph.Add(new DependencyComponent(parsed.Name, parsed.Version, "library", purl, "conan", Development: false, licenses.TryGetValue(parsed.Name, out string? license) ? new[] { license } : Array.Empty<string>(), path, null, num, num, null, relationship));
				dictionary[item.Name] = value3;
			}
		}
		foreach (JsonProperty item2 in nodes.EnumerateObject())
		{
			if (!dictionary.TryGetValue(item2.Name, out var value4) || !item2.Value.TryGetProperty("requires", out var value5))
			{
				continue;
			}
			foreach (JsonElement item3 in value5.EnumerateArray())
			{
				if (dictionary.TryGetValue(item3.GetString() ?? string.Empty, out var value6))
				{
					graph.AddInstanceEdge(value4, value6);
				}
			}
		}
	}

	private static void AnalyzeRequirements(JsonElement requirements, string path, string content, IReadOnlyDictionary<string, string> licenses, DependencyGraph graph)
	{
		int searchOffset = 0;
		foreach (JsonElement item in requirements.EnumerateArray())
		{
			string text = item.GetString() ?? string.Empty;
			if (PackageHelper.TryParseConanReference(text, out ConanReference parsed))
			{
				int? num = JsonHelper.FindValueLine(content, text, ref searchOffset);
				graph.Add(new DependencyComponent(parsed.Name, parsed.Version, "library", PackageHelper.ConanPurl(parsed.Name, parsed.Version), "conan", Development: false, licenses.TryGetValue(parsed.Name, out string? license) ? new[] { license } : Array.Empty<string>(), path, null, num, num));
			}
		}
	}
}
