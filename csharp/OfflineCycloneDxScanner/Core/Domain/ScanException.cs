using System;

namespace OfflineCycloneDxScanner;

public sealed class ScanException : Exception
{
	public ScanException(string message)
		: base(message)
	{
	}

	public ScanException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
