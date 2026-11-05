using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OfflineCycloneDxScanner;

public sealed class OfflineScanner(IEnumerable<IProjectAnalyzer> analyzers, IFileSystem fileSystem, IDiagnostics diagnostics)
{
	public CycloneDxReport Scan(ScanOptions options)
	{
		string input = Path.GetFullPath(options.InputPath);
		if (!fileSystem.DirectoryExists(input) && !fileSystem.FileExists(input))
		{
			throw new ScanException("Input path does not exist: " + input);
		}
		DependencyGraph dependencyGraph = new DependencyGraph();
		IProjectAnalyzer[] array = analyzers.Where((IProjectAnalyzer analyzer) => analyzer.CanAnalyze(input)).ToArray();
		if (array.Length == 0)
		{
			diagnostics.Warning("No supported Conan, Dart, Composer, Maven, or Gradle project files were found.");
		}
		IProjectAnalyzer[] array2 = array;
		foreach (IProjectAnalyzer projectAnalyzer in array2)
		{
			diagnostics.Info($"Analyzing {projectAnalyzer.Name} in offline mode: {options.Offline}");
			try
			{
				projectAnalyzer.Analyze(input, options, dependencyGraph, diagnostics);
			}
			catch (Exception ex) when (!(ex is ScanException))
			{
				diagnostics.Warning(projectAnalyzer.Name + " analysis failed and was skipped: " + ex.Message);
			}
		}
		if (!options.IncludeDevDependencies)
		{
			dependencyGraph.RemoveDevelopmentDependencies();
		}
		return new CycloneDxReport(input, dependencyGraph);
	}
}
