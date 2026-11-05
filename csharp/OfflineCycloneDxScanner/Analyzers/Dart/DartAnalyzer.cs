using System;
using System.Collections.Generic;

namespace OfflineCycloneDxScanner;

public sealed class DartAnalyzer : FileProjectAnalyzer
{
	public DartAnalyzer(IFileSystem fileSystem)
		: base("Dart", "pubspec.lock", "pub", fileSystem)
	{
	}

	public override void Analyze(string root, ScanOptions options, DependencyGraph graph, IDiagnostics diagnostics)
	{
		foreach (string item in base.FileSystem.FindFiles(root, base.FileName))
		{
			foreach (DartLockPackage item2 in DartLockParser.Parse(base.FileSystem.ReadAllText(item)))
			{
				graph.Add(new DependencyComponent(item2.Name, item2.Version, "library", PackageHelper.Purl(base.Ecosystem, item2.Name, item2.Version), base.Ecosystem, item2.Relationship.Contains("dev", StringComparison.OrdinalIgnoreCase), (IReadOnlyCollection<string>)(object)Array.Empty<string>(), item, null, null, null, null, DartRelationship(item2.Relationship)));
			}
		}
	}

	private static string? DartRelationship(string relationship)
	{
		if (1 == 0)
		{
		}
		string result;
		switch (relationship)
		{
		case "direct main":
		case "direct dev":
			result = "direct";
			break;
		case "transitive":
			result = "indirect";
			break;
		default:
			result = null;
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}
}
