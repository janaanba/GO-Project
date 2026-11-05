using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OfflineCycloneDxScanner;

public sealed class GradleAnalyzer : FileProjectAnalyzer
{
	private sealed record GradleDependency(string Group, string Artifact, string Version, bool Development, int Line);
	private sealed record GradlePomInfo(IReadOnlyCollection<string> Licenses, IReadOnlyCollection<(string Group, string Artifact, string Version)> Dependencies);

	public GradleAnalyzer(IFileSystem fileSystem)
		: base("Gradle", "gradle.lockfile", "gradle", fileSystem)
	{
	}

	public override void Analyze(string root, ScanOptions options, DependencyGraph graph, IDiagnostics diagnostics)
	{
		foreach (string item in base.FileSystem.FindFiles(root, base.FileName))
		{
			Dictionary<string, GradleDependency> dictionary = new Dictionary<string, GradleDependency>(StringComparer.Ordinal);
			int num = 0;
			foreach (GradleDependency item2 in Parse(base.FileSystem.ReadLines(item)))
			{
				string text = item2.Group + ":" + item2.Artifact;
				string key = text + "@" + item2.Version;
				if (!dictionary.TryGetValue(key, out var value))
				{
					dictionary[key] = item2;
					continue;
				}
				dictionary[key] = value with
				{
					Development = (value.Development && item2.Development),
					Line = Math.Min(value.Line, item2.Line)
				};
			}
			Dictionary<string, GradlePomInfo> pomInfo = dictionary.Values
				.Select(dependency => (Key: dependency.Group + ":" + dependency.Artifact + ":" + dependency.Version, Info: ReadPom(dependency)))
			.Where(value => value.Info != null)
			.ToDictionary(value => value.Key, value => value.Info!, StringComparer.Ordinal);
			foreach (GradleDependency value2 in dictionary.Values)
			{
				string name = value2.Group + ":" + value2.Artifact;
				string key = value2.Group + ":" + value2.Artifact + ":" + value2.Version;
				IReadOnlyCollection<string> licenses = pomInfo.TryGetValue(key, out GradlePomInfo? info) ? info.Licenses : Array.Empty<string>();
				graph.Add(new DependencyComponent(name, value2.Version, "library", PackageHelper.Purl(base.Ecosystem, name, value2.Version), base.Ecosystem, value2.Development, licenses, item, null, value2.Line, value2.Line));
				num++;
			}
			HashSet<string> packageKeys = dictionary.Values
				.Select(dependency => dependency.Group + ":" + dependency.Artifact + ":" + dependency.Version)
				.ToHashSet(StringComparer.Ordinal);
			foreach (GradleDependency source in dictionary.Values)
			{
				string sourceKey = source.Group + ":" + source.Artifact + ":" + source.Version;
				if (!pomInfo.TryGetValue(sourceKey, out GradlePomInfo? info))
				{
					continue;
				}
				string sourcePurl = PackageHelper.Purl(base.Ecosystem, source.Group + "/" + source.Artifact, source.Version);
				foreach ((string Group, string Artifact, string Version) dependency in info.Dependencies)
				{
					string targetKey = dependency.Group + ":" + dependency.Artifact + ":" + dependency.Version;
					if (packageKeys.Contains(targetKey))
					{
						graph.AddEdge(sourcePurl, PackageHelper.Purl(base.Ecosystem, dependency.Group + "/" + dependency.Artifact, dependency.Version));
					}
				}
			}
			diagnostics.Info($"Parsed {num} Gradle lock entries from {Path.GetFileName(item)}.");
		}
	}

	private GradlePomInfo? ReadPom(GradleDependency dependency)
	{
		string? cacheRoot = Environment.GetEnvironmentVariable("GRADLE_USER_HOME");
		cacheRoot ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gradle");
		string directory = Path.Combine(cacheRoot, "caches", "modules-2", "files-2.1", dependency.Group, dependency.Artifact, dependency.Version);
		if (!Directory.Exists(directory))
		{
			return null;
		}
		string? pomPath = Directory.EnumerateFiles(directory, "*.pom", SearchOption.AllDirectories).FirstOrDefault();
		if (pomPath == null)
		{
			return null;
		}

		XDocument document = XDocument.Load(pomPath);
		XNamespace ns = document.Root?.Name.Namespace ?? XNamespace.None;
		IReadOnlyCollection<string> licenses = document.Descendants(ns + "license")
			.Select(license => license.Element(ns + "name")?.Value.Trim())
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.Cast<string>()
			.Distinct(StringComparer.Ordinal)
			.ToArray();
		IReadOnlyCollection<(string Group, string Artifact, string Version)> dependencies = document.Descendants(ns + "dependency")
			.Select(dependencyElement =>
			(
				Group: dependencyElement.Element(ns + "groupId")?.Value.Trim() ?? string.Empty,
				Artifact: dependencyElement.Element(ns + "artifactId")?.Value.Trim() ?? string.Empty,
				Version: dependencyElement.Element(ns + "version")?.Value.Trim() ?? string.Empty
			))
			.Where(dependencyValue => dependencyValue.Group.Length > 0 && dependencyValue.Artifact.Length > 0 && dependencyValue.Version.Length > 0)
			.Distinct()
			.ToArray();
		return new GradlePomInfo(licenses, dependencies);
	}

	private static IEnumerable<GradleDependency> Parse(IEnumerable<string> lines)
	{
		int lineNumber = 0;
		foreach (string rawLine in lines)
		{
			lineNumber++;
			string line = rawLine.Trim();
			if (line.Length == 0 || line.StartsWith('#'))
			{
				continue;
			}
			string[] parts = line.Split('=', 2);
			string[] coordinate = parts[0].Split(':');
			if (coordinate.Length == 3)
			{
				string[] configurations = ((parts.Length == 2) ? parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>());
				yield return new GradleDependency(Development: configurations.Length != 0 && configurations.All((string configuration) => configuration.StartsWith("test", StringComparison.Ordinal)), Group: coordinate[0], Artifact: coordinate[1], Version: coordinate[2], Line: lineNumber);
			}
		}
	}
}
