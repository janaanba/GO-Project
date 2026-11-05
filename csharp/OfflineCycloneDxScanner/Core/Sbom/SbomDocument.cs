using System.Collections.Generic;

namespace OfflineCycloneDxScanner;

public sealed record SbomDocument(string RootReference, SbomComponent Root, IReadOnlyCollection<SbomComponent> Components, IReadOnlyCollection<SbomRelationship> Relationships);
