using System.Collections.Generic;

namespace OfflineCycloneDxScanner;

internal sealed record MavenResolvedDependency(string Name, string Version, string Scope, bool Optional, bool Development, IReadOnlySet<string> Exclusions, int? StartLine, int? EndLine);
