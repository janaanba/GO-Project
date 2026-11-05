using System.Collections.Generic;

namespace OfflineCycloneDxScanner;

public sealed record SbomRelationship(string Source, IReadOnlyCollection<string> Targets);
