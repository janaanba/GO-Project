using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OfflineCycloneDxScanner;

public sealed class MavenLocalRepositoryReader
{
	private sealed record MavenDependency(string Name, string Version, string Scope, bool Optional, IReadOnlySet<string> Exclusions, bool Development, int? StartLine, int? EndLine);

	private readonly string repositoryPath;

	private readonly IReadOnlySet<string> activeProfiles;

	private readonly IDiagnostics diagnostics;

	public MavenLocalRepositoryReader(string? repositoryPath, IDiagnostics diagnostics, IReadOnlySet<string>? activeProfiles = null)
	{
				this.diagnostics = diagnostics;
		this.repositoryPath = ResolveRepositoryPath(repositoryPath);
		this.activeProfiles = activeProfiles ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	}

	public void AddDependencyEdges(IReadOnlyDictionary<string, string> components, DependencyGraph graph)
	{
		Dictionary<string, string> selectedVersions = (from instanceId in components.Values
			select graph.Components.FirstOrDefault((DependencyComponent value) => value.InstanceId == instanceId) into dependencyComponent2
			where (object)dependencyComponent2 != null
			select dependencyComponent2).Cast<DependencyComponent>().GroupBy<DependencyComponent, string>((DependencyComponent dependencyComponent2) => dependencyComponent2.Name, StringComparer.Ordinal).ToDictionary<IGrouping<string, DependencyComponent>, string, string>((IGrouping<string, DependencyComponent> group) => group.Key, (IGrouping<string, DependencyComponent> group) => group.First().Version, StringComparer.Ordinal);
		foreach (KeyValuePair<string, string> component in components)
		{
			DependencyComponent dependencyComponent = graph.Components.LastOrDefault((DependencyComponent value) => value.InstanceId == component.Value);
			if ((object)dependencyComponent != null)
			{
				ReadTransitiveDependencies(component.Value, graph, new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), selectedVersions);
			}
		}
	}

	private void ReadTransitiveDependencies(string source, DependencyGraph graph, HashSet<string> visited, IReadOnlySet<string> inheritedExclusions, IDictionary<string, string> selectedVersions)
	{
		string item = source + "|" + string.Join(",", inheritedExclusions.OrderBy<string, string>((string result) => result, StringComparer.Ordinal));
		if (!visited.Add(item))
		{
			return;
		}
		DependencyComponent dependencyComponent = graph.Components.SingleOrDefault((DependencyComponent dependencyComponent2) => dependencyComponent2.InstanceId == source) ?? graph.Components.LastOrDefault((DependencyComponent dependencyComponent2) => dependencyComponent2.Purl == source);
		if ((object)dependencyComponent == null)
		{
			return;
		}
		string[] array = dependencyComponent.Name.Split(':', 2);
		if (array.Length != 2)
		{
			return;
		}
		string text = Path.Combine(repositoryPath, array[0].Replace('.', Path.DirectorySeparatorChar), array[1], dependencyComponent.Version, array[1] + "-" + dependencyComponent.Version + ".pom");
		if (!File.Exists(text))
		{
			return;
		}
		try
		{
			foreach (MavenDependency dependency in ReadPomDependencies(text, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
			{
				if (IsRuntimeDependency(dependency.Scope) && !dependency.Optional && !inheritedExclusions.Any((string exclusion) => MatchesExclusion(exclusion, dependency.Name)))
				{
					HashSet<string> inheritedExclusions2 = inheritedExclusions.Concat(dependency.Exclusions).ToHashSet<string>(StringComparer.Ordinal);
					if (!selectedVersions.TryGetValue(dependency.Name, out string value))
					{
						value = (selectedVersions[dependency.Name] = dependency.Version);
					}
					string purl = PackageHelper.Purl("maven", dependency.Name, value);
					string text2 = (graph.Components.Any((DependencyComponent dependencyComponent2) => dependencyComponent2.Purl == purl) ? graph.Components.Last((DependencyComponent dependencyComponent2) => dependencyComponent2.Purl == purl).InstanceId : graph.Add(new DependencyComponent(dependency.Name, value, "library", purl, "maven", dependency.Development, (IReadOnlyCollection<string>)(object)Array.Empty<string>(), text, null, dependency.StartLine, dependency.EndLine)));
					graph.AddInstanceEdge(source, text2);
					ReadTransitiveDependencies(text2, graph, visited, inheritedExclusions2, selectedVersions);
				}
			}
		}
		catch (Exception ex)
		{
						diagnostics.Warning("Unable to read local Maven metadata " + text + ": " + ex.Message);
		}
	}

	private IReadOnlyCollection<MavenDependency> ReadPomDependencies(string path, HashSet<string> visited)
	{
		if (!visited.Add(path))
		{
			return (IReadOnlyCollection<MavenDependency>)(object)Array.Empty<MavenDependency>();
		}
				return (IReadOnlyCollection<MavenDependency>)(object)MavenPomModel.Load(path, repositoryPath, diagnostics, activeProfiles).Dependencies.Select((MavenResolvedDependency dependency) => new MavenDependency(dependency.Name, dependency.Version, dependency.Scope, dependency.Optional, dependency.Exclusions, dependency.Development, dependency.StartLine, dependency.EndLine)).ToArray();
	}

	private static bool IsRuntimeDependency(string scope)
	{
		switch (scope)
		{
		default:
			if (scope.Length != 0)
			{
				break;
			}
			goto case "compile";
		case "compile":
		case "runtime":
			return true;
		case null:
			break;
		}
		return false;
	}

	private static bool MatchesExclusion(string exclusion, string dependency)
	{
		string[] array = exclusion.Split(':', 2);
		string[] array2 = dependency.Split(':', 2);
		return array.Length == 2 && array2.Length == 2 && (array[0] == "*" || array[0] == array2[0]) && (array[1] == "*" || array[1] == array2[1]);
	}

	public static string ResolveRepositoryPath(string? configuredPath)
	{
		return ResolveRepositoryPath(configuredPath, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
	}

	public static string ResolveRepositoryPath(string? configuredPath, string userProfile)
	{
		if (!string.IsNullOrWhiteSpace(configuredPath))
		{
			return configuredPath;
		}
		string path = Path.Combine(userProfile, ".m2");
		string text = Path.Combine(path, "settings.xml");
		if (File.Exists(text))
		{
			try
			{
				XDocument xDocument = XDocument.Load(text);
				XNamespace xNamespace = xDocument.Root?.Name.Namespace ?? XNamespace.None;
				string text2 = xDocument.Root?.Element(xNamespace + "localRepository")?.Value.Trim();
				if (!string.IsNullOrWhiteSpace(text2))
				{
					return Environment.ExpandEnvironmentVariables(text2.Replace("${user.home}", userProfile, StringComparison.Ordinal));
				}
			}
			catch
			{
			}
		}
		return Path.Combine(path, "repository");
	}
}
