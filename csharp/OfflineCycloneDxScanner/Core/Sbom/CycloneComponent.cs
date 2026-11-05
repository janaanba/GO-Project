using System.Collections.Generic;

namespace OfflineCycloneDxScanner;

public sealed record CycloneComponent(string Type, string Name, string? Version, string BomRef, IReadOnlyCollection<CycloneLicense> Licenses, IReadOnlyCollection<CycloneProperty> Properties, IReadOnlyCollection<CycloneHash> Hashes, string? Group = null, string? Supplier = null, string? Purl = null);
