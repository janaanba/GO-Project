using System;

namespace OfflineCycloneDxScanner;

public sealed record CycloneMetadata(DateTimeOffset Timestamp, CycloneTools Tools, CycloneComponent? Component);
