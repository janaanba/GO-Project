using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OfflineCycloneDxScanner;

public sealed class SbomEncoder
{
	public SbomDocument Encode(CycloneDxReport report)
	{
		string text = "urn:uuid:" + StableUuid(report.InputPath);
		SbomComponent root = new SbomComponent(text, "application", Path.GetFileName(report.InputPath), string.Empty, string.Empty, report.InputPath, (IReadOnlyCollection<string>)(object)Array.Empty<string>(), null, null, null, string.Empty, null, null, null, null, null, null, null, null, IsRoot: true);
		Dictionary<string, string> bomReferences = report.Graph.Components.ToDictionary<DependencyComponent, string, string>((DependencyComponent component) => component.InstanceId, (DependencyComponent component) => PackageReference(component, report.Graph), StringComparer.Ordinal);
		SbomComponent[] packages = report.Graph.Components.Select((DependencyComponent component) => new SbomComponent(bomReferences[component.InstanceId], component.Type, component.Name, component.Version, component.Purl, component.SourceFile, component.Licenses, component.Sha256, component.StartLine, component.EndLine, component.Ecosystem, component.Relationship, component.Group, component.Supplier, component.Files, component.Qualifiers, component.Properties, component.SourceName, component.SourceVersion, component.IsRoot, component.IsWorkspace, component.Vulnerabilities)).OrderBy<SbomComponent, string>((SbomComponent component) => component.BomReference, StringComparer.Ordinal).ToArray();
		Dictionary<string, string> inputReferences = packages.Select(component => component.SourceFile).Distinct(StringComparer.Ordinal).ToDictionary(source => source, SourceReference, StringComparer.Ordinal);
		SbomComponent[] inputs = inputReferences.Select(pair => new SbomComponent(pair.Value, "library", Path.GetFileName(pair.Key), string.Empty, string.Empty, pair.Key, (IReadOnlyCollection<string>)(object)Array.Empty<string>(), null, null, null, packages.First(component => string.Equals(component.SourceFile, pair.Key, StringComparison.Ordinal)).Ecosystem, null, Properties: null, IsInputFile: true)).OrderBy(component => component.BomReference, StringComparer.Ordinal).ToArray();
		SbomComponent[] array = inputs.Concat(packages).ToArray();
		List<SbomRelationship> list = packages.Select((SbomComponent component) => new SbomRelationship(component.BomReference, InstanceTargets(component, report.Graph, bomReferences))).ToList();
		list.AddRange(inputs.Select(input => new SbomRelationship(input.BomReference, packages.Where(package => string.Equals(package.SourceFile, input.SourceFile, StringComparison.Ordinal)).Select(package => package.BomReference).OrderBy(value => value, StringComparer.Ordinal).ToArray())));
		list.Insert(0, new SbomRelationship(text, inputs.Length == 0 ? packages.Select(component => component.BomReference).ToArray() : inputs.Select(component => component.BomReference).OrderBy(value => value, StringComparer.Ordinal).ToArray()));
		return new SbomDocument(text, root, (IReadOnlyCollection<SbomComponent>)(object)array, list);
	}

	private static string SourceReference(string source)
	{
		return "urn:uuid:" + StableUuid("source:" + Path.GetFullPath(source));
	}

	private static string PackageReference(DependencyComponent component, DependencyGraph graph)
	{
		return graph.PurlIndex.TryGetValue(component.Purl, out IReadOnlyList<string>? instances) && instances.Count == 1
			? component.Purl
			: BomReference(component.InstanceId);
	}

	private static string StableUuid(string value)
	{
		byte[] array = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(value)));
		return new Guid(array[..16]).ToString("D");
	}

	private static IReadOnlyCollection<string> InstanceTargets(SbomComponent component, DependencyGraph graph, IReadOnlyDictionary<string, string> bomReferences)
	{
		string text = graph.Components.FirstOrDefault((DependencyComponent dependencyComponent) => bomReferences.GetValueOrDefault(dependencyComponent.InstanceId) == component.BomReference)?.InstanceId;
		HashSet<string> value;
		return (IReadOnlyCollection<string>)(object)((text == null || !graph.InstanceEdges.TryGetValue(text, out value)) ? Array.Empty<string>() : (from target in value.Where(bomReferences.ContainsKey)
			select bomReferences[target]).OrderBy<string, string>((string target) => target, StringComparer.Ordinal).ToArray());
	}

	private static string BomReference(string instanceId)
	{
		Guid result;
		return Guid.TryParse(instanceId, out result) ? $"urn:uuid:{result:D}" : ("urn:uuid:" + StableUuid(instanceId));
	}
}
