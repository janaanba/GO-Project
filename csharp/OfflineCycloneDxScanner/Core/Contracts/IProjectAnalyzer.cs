namespace OfflineCycloneDxScanner;

public interface IProjectAnalyzer
{
	string Name { get; }

	bool CanAnalyze(string inputDirectory);

	void Analyze(string inputDirectory, ScanOptions options, DependencyGraph graph, IDiagnostics diagnostics);
}
