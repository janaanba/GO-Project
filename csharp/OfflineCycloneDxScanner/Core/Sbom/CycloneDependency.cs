using System.Collections.Generic;

namespace OfflineCycloneDxScanner;

public sealed record CycloneDependency(string Ref, IReadOnlyCollection<string> DependsOn);
