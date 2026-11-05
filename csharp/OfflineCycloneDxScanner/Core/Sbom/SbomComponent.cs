using System.Collections.Generic;

namespace OfflineCycloneDxScanner;

public sealed record SbomComponent(string BomReference, string Type, string Name, string Version, string Purl, string SourceFile, IReadOnlyCollection<string> Licenses, string? Sha256, int? StartLine, int? EndLine, string Ecosystem, string? Relationship, string? Group = null, string? Supplier = null, IReadOnlyCollection<string>? Files = null, IReadOnlyDictionary<string, string>? Qualifiers = null, IReadOnlyDictionary<string, string>? Properties = null, string? SourceName = null, string? SourceVersion = null, bool IsRoot = false, bool IsWorkspace = false, IReadOnlyCollection<string>? Vulnerabilities = null, bool IsInputFile = false);
