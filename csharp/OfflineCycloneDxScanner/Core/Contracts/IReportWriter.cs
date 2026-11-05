namespace OfflineCycloneDxScanner;

public interface IReportWriter
{
	string Write(CycloneDxReport report);
}
