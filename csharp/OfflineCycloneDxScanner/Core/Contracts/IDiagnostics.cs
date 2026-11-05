namespace OfflineCycloneDxScanner;

public interface IDiagnostics
{
	void Info(string message);

	void Warning(string message);

	void Error(string message);
}
