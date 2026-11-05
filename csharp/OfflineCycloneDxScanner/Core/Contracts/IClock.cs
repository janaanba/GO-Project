using System;

namespace OfflineCycloneDxScanner;

public interface IClock
{
	DateTimeOffset UtcNow { get; }
}
