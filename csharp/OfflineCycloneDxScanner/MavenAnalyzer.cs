using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OfflineCycloneDxScanner;

public sealed class MavenAnalyzer : FileProjectAnalyzer
{
	private readonly string userProfile;

	public MavenAnalyzer(IFileSystem fileSystem, string? userProfile = null)
		: base("Maven", "pom.xml", "maven", fileSystem)
	{
		this.userProfile = userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
	}

	public override void Analyze(string root, ScanOptions options, DependencyGraph graph, IDiagnostics diagnostics)
	{
		string repositoryPath = MavenLocalRepositoryReader.ResolveRepositoryPath(options.MavenRepository, userProfile);
		IReadOnlySet<string> activeProfiles = ReadActiveProfiles(base.FileSystem, diagnostics, userProfile);
		(string, MavenPomModel)[] array = (from path in base.FileSystem.FindFiles(root, base.FileName)
			select (Path: path, Model: MavenPomModel.Load(path, repositoryPath, diagnostics, activeProfiles))).ToArray();
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		(string, MavenPomModel)[] array2 = array;
		for (int num = 0; num < array2.Length; num++)
		{
			(string, MavenPomModel) tuple = array2[num];
			string text = ProjectReference(tuple.Item2);
			if (text != null)
			{
				graph.Add(new DependencyComponent(tuple.Item2.GroupId + ":" + tuple.Item2.ArtifactId, tuple.Item2.Version, "library", text, "maven", Development: false, tuple.Item2.Licenses, tuple.Item1));
				dictionary[Path.GetFullPath(tuple.Item1)] = text;
			}
		}
		(string, MavenPomModel)[] array3 = array;
		for (int num2 = 0; num2 < array3.Length; num2++)
		{
			(string, MavenPomModel) tuple2 = array3[num2];
			string item = tuple2.Item1;
			MavenPomModel item2 = tuple2.Item2;
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (MavenResolvedDependency dependency in item2.Dependencies)
			{
				string purl = PackageHelper.Purl("maven", dependency.Name, dependency.Version);
				string value = graph.Add(new DependencyComponent(dependency.Name, dependency.Version, "library", purl, "maven", dependency.Development, (IReadOnlyCollection<string>)(object)Array.Empty<string>(), item, null, dependency.StartLine, dependency.EndLine));
				dictionary2[dependency.Name] = value;
			}
			string projectReference = ProjectReference(item2);
			if (projectReference != null)
			{
				string text2 = graph.Components.LastOrDefault((DependencyComponent component) => component.Purl == projectReference)?.InstanceId;
				if (text2 != null)
				{
					foreach (string value2 in dictionary2.Values)
					{
						graph.AddInstanceEdge(text2, value2);
					}
				}
				foreach (string module in item2.Modules)
				{
					string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(item) ?? root, module, base.FileName));
					if (dictionary.TryGetValue(fullPath, out var moduleReference))
					{
						string text3 = graph.Components.LastOrDefault((DependencyComponent component) => component.Purl == moduleReference)?.InstanceId;
						if (text2 != null && text3 != null)
						{
							graph.AddInstanceEdge(text2, text3);
						}
					}
				}
			}
			if (options.Offline)
			{
				new MavenLocalRepositoryReader(repositoryPath, diagnostics, activeProfiles).AddDependencyEdges(dictionary2, graph);
			}
		}
	}

	private static IReadOnlySet<string> ReadActiveProfiles(IFileSystem fileSystem, IDiagnostics diagnostics, string userProfile)
	{
		string text = Path.Combine(userProfile, ".m2", "settings.xml");
		if (!fileSystem.FileExists(text))
		{
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}
		try
		{
			return (from element in XDocument.Load(text).Descendants("activeProfile")
				select element.Value.Trim() into value
				where value.Length > 0
				select value).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		}
		catch (Exception ex)
		{
			diagnostics.Warning("Unable to read Maven settings profiles: " + ex.Message);
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}
	}

	private static string? ProjectReference(MavenPomModel model)
	{
		if (string.IsNullOrWhiteSpace(model.GroupId) || string.IsNullOrWhiteSpace(model.ArtifactId) || string.IsNullOrWhiteSpace(model.Version))
		{
			return null;
		}
		return PackageHelper.Purl("maven", model.GroupId + ":" + model.ArtifactId, model.Version);
	}
}
