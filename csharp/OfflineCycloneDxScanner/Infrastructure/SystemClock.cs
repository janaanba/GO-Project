using System;

namespace OfflineCycloneDxScanner;

public sealed class SystemClock : IClock
{
	public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
