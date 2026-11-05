namespace OfflineCycloneDxScanner;

public sealed record ConanReference(string Name, string Version, string? User, string? Channel, string? Revision, string? PackageId, string? PackageRevision, string Raw);
