using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace OfflineCycloneDxScanner;

internal static class DartLockParser
{
	public static IEnumerable<DartLockPackage> Parse(string content)
	{
		YamlStream yaml = new YamlStream();
		using StringReader reader = new StringReader(content);
		new DeserializerBuilder().WithDuplicateKeyChecking().Build().Deserialize<object>(content);
		yaml.Load(reader);
		YamlMappingNode root = default(YamlMappingNode);
		int num;
		YamlNode rootNode;
		if (yaml.Documents.Count != 0)
		{
			rootNode = yaml.Documents[0].RootNode;
			root = rootNode as YamlMappingNode;
			num = ((root == null) ? 1 : 0);
		}
		else
		{
			num = 1;
		}
		if (num != 0)
		{
			yield break;
		}
		Dictionary<string, string> sdkConstraints = new Dictionary<string, string>(StringComparer.Ordinal);
		rootNode = Mapping(root, "sdks");
		if (rootNode is YamlMappingNode sdkMapping)
		{
			foreach (KeyValuePair<YamlNode, YamlNode> entry in sdkMapping.Children)
			{
				string name = Scalar(entry.Key);
				string constraint = Scalar(entry.Value);
				if (name != null && constraint != null)
				{
					sdkConstraints[name] = constraint;
				}
			}
		}
		rootNode = Mapping(root, "packages");
		if (!(rootNode is YamlMappingNode packages))
		{
			yield break;
		}
		YamlMappingNode package = default(YamlMappingNode);
		foreach (KeyValuePair<YamlNode, YamlNode> packageEntry in packages.Children)
		{
			string name2 = Scalar(packageEntry.Key);
			int num2;
			if (name2 != null)
			{
				rootNode = packageEntry.Value;
				package = rootNode as YamlMappingNode;
				num2 = ((package == null) ? 1 : 0);
			}
			else
			{
				num2 = 1;
			}
			if (num2 == 0)
			{
				string version = Scalar(package, "version");
				if (version != null)
				{
					string source = Scalar(package, "source") ?? string.Empty;
					string description = Scalar(package, "description");
					yield return new DartLockPackage(Relationship: Scalar(package, "dependency") ?? "unknown", Name: name2, Version: ResolveSdkVersion(version, source, description, sdkConstraints), Source: source, Description: description);
					package = null;
				}
			}
		}
	}

	private static string ResolveSdkVersion(string version, string source, string? description, IReadOnlyDictionary<string, string> sdkConstraints)
	{
		if (!string.Equals(version, "0.0.0", StringComparison.Ordinal) || !string.Equals(source, "sdk", StringComparison.Ordinal) || description == null || !sdkConstraints.TryGetValue(description, out string value))
		{
			return version;
		}
		return DartVersionConstraints.MinimumVersion(value) ?? version;
	}

	private static YamlNode? Mapping(YamlMappingNode mapping, string key)
	{
		return mapping.Children.FirstOrDefault<KeyValuePair<YamlNode, YamlNode>>((KeyValuePair<YamlNode, YamlNode> entry) => string.Equals(Scalar(entry.Key), key, StringComparison.Ordinal)).Value;
	}

	private static string? Scalar(YamlMappingNode mapping, string key)
	{
		YamlNode yamlNode = Mapping(mapping, key);
		return (yamlNode is YamlScalarNode yamlScalarNode) ? yamlScalarNode.Value : null;
	}

	private static string? Scalar(YamlNode node)
	{
		return (node is YamlScalarNode yamlScalarNode) ? yamlScalarNode.Value : null;
	}
}
