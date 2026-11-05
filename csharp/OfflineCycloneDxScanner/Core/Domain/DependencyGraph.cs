using System;
using System.Collections.Generic;
using System.Linq;

namespace OfflineCycloneDxScanner;

public sealed class DependencyGraph
{
	private readonly List<DependencyComponent> components = new List<DependencyComponent>();

	private readonly Dictionary<string, HashSet<string>> instanceEdges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

	private readonly Dictionary<string, HashSet<string>> edges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

	private readonly Dictionary<string, List<string>> purlIndex = new Dictionary<string, List<string>>(StringComparer.Ordinal);

	public IReadOnlyCollection<DependencyComponent> Components => components;

	public IReadOnlyDictionary<string, HashSet<string>> Edges => edges;

	public IReadOnlyDictionary<string, HashSet<string>> InstanceEdges => instanceEdges;

	public IReadOnlyDictionary<string, IReadOnlyList<string>> PurlIndex => purlIndex.ToDictionary<KeyValuePair<string, List<string>>, string, IReadOnlyList<string>>((KeyValuePair<string, List<string>> pair) => pair.Key, (KeyValuePair<string, List<string>> pair) => pair.Value, StringComparer.Ordinal);

	public string Add(DependencyComponent component)
	{
		string instanceId = component.InstanceId ?? Guid.NewGuid().ToString("D");
		while (components.Any((DependencyComponent existing) => existing.InstanceId == instanceId))
		{
			instanceId = Guid.NewGuid().ToString("D");
		}
		DependencyComponent dependencyComponent = component with
		{
			InstanceId = instanceId
		};
		components.Add(dependencyComponent);
		if (!edges.ContainsKey(dependencyComponent.Purl))
		{
			edges[dependencyComponent.Purl] = new HashSet<string>(StringComparer.Ordinal);
		}
		if (!purlIndex.TryGetValue(dependencyComponent.Purl, out List<string> value))
		{
			value = (purlIndex[dependencyComponent.Purl] = new List<string>());
		}
		value.Add(instanceId);
		return instanceId;
	}

	public void AddEdge(string source, string target)
	{
		string text = purlIndex.GetValueOrDefault(source)?.LastOrDefault();
		string text2 = purlIndex.GetValueOrDefault(target)?.LastOrDefault();
		if (text != null && text2 != null)
		{
			AddInstanceEdge(text, text2);
			edges[source].Add(target);
		}
	}

	public void AddInstanceEdge(string sourceInstance, string targetInstance)
	{
		DependencyComponent dependencyComponent = components.FirstOrDefault((DependencyComponent component) => component.InstanceId == sourceInstance);
		DependencyComponent dependencyComponent2 = components.FirstOrDefault((DependencyComponent component) => component.InstanceId == targetInstance);
		if ((object)dependencyComponent != null && (object)dependencyComponent2 != null)
		{
			if (!instanceEdges.TryGetValue(sourceInstance, out HashSet<string> value))
			{
				value = (instanceEdges[sourceInstance] = new HashSet<string>(StringComparer.Ordinal));
			}
			value.Add(targetInstance);
			edges[dependencyComponent.Purl].Add(dependencyComponent2.Purl);
		}
	}

	public void RemoveDevelopmentDependencies()
	{
		HashSet<string> developmentIds = (from component in components
			where component.Development && !string.Equals(component.Ecosystem, "pub", StringComparison.OrdinalIgnoreCase)
			select component.InstanceId).ToHashSet<string>(StringComparer.Ordinal);
		components.RemoveAll((DependencyComponent component) => developmentIds.Contains(component.InstanceId));
		string[] array = instanceEdges.Keys.ToArray();
		foreach (string text in array)
		{
			if (developmentIds.Contains(text))
			{
				instanceEdges.Remove(text);
			}
			else
			{
				instanceEdges[text].ExceptWith(developmentIds);
			}
		}
		string[] array2 = edges.Keys.ToArray();
		foreach (string source in array2)
		{
			if (!components.Any((DependencyComponent component) => component.Purl == source))
			{
				edges.Remove(source);
			}
		}
		string[] array3 = purlIndex.Keys.ToArray();
		foreach (string key in array3)
		{
			purlIndex[key].RemoveAll(developmentIds.Contains);
			if (purlIndex[key].Count == 0)
			{
				purlIndex.Remove(key);
			}
		}
	}
}
