using System.Collections.Generic;

namespace OfflineCycloneDxScanner;

public sealed record CycloneDocument(string BomFormat, string SpecVersion, string SerialNumber, CycloneMetadata Metadata, IReadOnlyCollection<CycloneComponent> Components, IReadOnlyCollection<CycloneDependency> Dependencies);
