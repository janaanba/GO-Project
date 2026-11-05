using System.Collections.Generic;

namespace OfflineCycloneDxScanner;

public sealed record DependencyComponent(string Name, string Version, string Type, string Purl, string Ecosystem, bool Development, IReadOnlyCollection<string> Licenses, string SourceFile, string? Sha256 = null, int? StartLine = null, int? EndLine = null, string? InstanceId = null, string? Relationship = null, string? Group = null, string? Supplier = null, IReadOnlyCollection<string>? Files = null, IReadOnlyDictionary<string, string>? Qualifiers = null, IReadOnlyDictionary<string, string>? Properties = null, string? SourceName = null, string? SourceVersion = null, bool IsRoot = false, bool IsWorkspace = false, IReadOnlyCollection<string>? Vulnerabilities = null);
