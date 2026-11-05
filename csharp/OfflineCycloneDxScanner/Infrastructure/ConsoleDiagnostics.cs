using System;

namespace OfflineCycloneDxScanner;

public sealed class ConsoleDiagnostics : IDiagnostics
{
	public void Info(string message)
	{
		Console.Error.WriteLine("INFO: " + message);
	}

	public void Warning(string message)
	{
		Console.Error.WriteLine("WARN: " + message);
	}

	public void Error(string message)
	{
		Console.Error.WriteLine("ERROR: " + message);
	}
}
