using System;

namespace OfflineCycloneDxScanner;

public sealed class GuidIdentifierFactory : IIdentifierFactory
{
	public string CreateSerialNumber()
	{
		return $"urn:uuid:{Guid.NewGuid()}";
	}
}
