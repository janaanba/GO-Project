using System;
using System.Linq;

namespace OfflineCycloneDxScanner;

public class FileProjectAnalyzer : IProjectAnalyzer
{
	protected IFileSystem FileSystem { get; }

	protected string FileName { get; }

	protected string Ecosystem { get; }

	public string Name { get; }

	public FileProjectAnalyzer(string name, string fileName, string ecosystem, IFileSystem fileSystem)
	{
		FileSystem = fileSystem;
		FileName = fileName;
		Ecosystem = ecosystem;
		Name = name;
	}

	public bool CanAnalyze(string inputDirectory)
	{
		return FileSystem.FindFiles(inputDirectory, FileName).Any();
	}

	public virtual void Analyze(string inputDirectory, ScanOptions options, DependencyGraph graph, IDiagnostics diagnostics)
	{
		throw new NotSupportedException("Analyzer " + Name + " does not implement analysis.");
	}
}
