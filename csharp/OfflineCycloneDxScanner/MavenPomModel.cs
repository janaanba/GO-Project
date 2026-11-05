using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OfflineCycloneDxScanner;

internal sealed class MavenPomModel
{
	public string Path { get; }

	public string GroupId { get; }

	public string ArtifactId { get; }

	public string Version { get; }

	public IReadOnlyDictionary<string, string> Properties { get; }

	public IReadOnlyDictionary<string, string> DependencyManagement { get; }

	public IReadOnlyList<string> Modules { get; }

	public IReadOnlyList<MavenResolvedDependency> Dependencies { get; }

	public IReadOnlyCollection<string> Licenses { get; }

	private MavenPomModel(string path, string groupId, string artifactId, string version, IReadOnlyDictionary<string, string> properties, IReadOnlyDictionary<string, string> dependencyManagement, IReadOnlyList<string> modules, IReadOnlyList<MavenResolvedDependency> dependencies, IReadOnlyCollection<string> licenses)
	{
		Path = path;
		GroupId = groupId;
		ArtifactId = artifactId;
		Version = version;
		Properties = properties;
		DependencyManagement = dependencyManagement;
		Modules = modules;
		Dependencies = dependencies;
		Licenses = licenses;
	}

	public static MavenPomModel Load(string path, string? repositoryPath, IDiagnostics diagnostics, IReadOnlySet<string>? activeProfiles = null)
	{
		return Load(path, repositoryPath, diagnostics, new HashSet<string>(StringComparer.OrdinalIgnoreCase), activeProfiles ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase));
	}

	private static MavenPomModel Load(string path, string? repositoryPath, IDiagnostics diagnostics, ISet<string> visited, IReadOnlySet<string> activeProfiles)
	{
		string fullPath = System.IO.Path.GetFullPath(path);
		if (!visited.Add(fullPath))
		{
			return Empty(fullPath);
		}
		XDocument xDocument = XDocument.Load(fullPath);
		XElement xElement = xDocument.Root ?? throw new InvalidDataException("Maven POM has no root element: " + fullPath);
		XNamespace ns = xElement.Name.Namespace;
		XElement xElement2 = xElement.Element(ns + "parent");
		MavenPomModel mavenPomModel = TryLoadParent(fullPath, xElement2, repositoryPath, diagnostics, visited, activeProfiles);
		string text = Value(xElement.Element(ns + "groupId")) ?? Value(xElement2?.Element(ns + "groupId")) ?? mavenPomModel?.GroupId ?? string.Empty;
		string text2 = Value(xElement.Element(ns + "artifactId")) ?? string.Empty;
		string text3 = Value(xElement.Element(ns + "version")) ?? Value(xElement2?.Element(ns + "version")) ?? mavenPomModel?.Version ?? string.Empty;
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["project.groupId"] = text,
			["project.artifactId"] = text2,
			["project.version"] = text3,
			["pom.groupId"] = text,
			["pom.artifactId"] = text2,
			["pom.version"] = text3
		};
		if (mavenPomModel != null)
		{
			foreach (KeyValuePair<string, string> property in mavenPomModel.Properties)
			{
				dictionary[property.Key] = property.Value;
			}
			dictionary["project.groupId"] = text;
			dictionary["project.artifactId"] = text2;
			dictionary["project.version"] = text3;
			dictionary["parent.groupId"] = mavenPomModel.GroupId;
			dictionary["parent.artifactId"] = mavenPomModel.ArtifactId;
			dictionary["parent.version"] = mavenPomModel.Version;
		}
		foreach (XElement item in xElement.Element(ns + "properties")?.Elements() ?? Enumerable.Empty<XElement>())
		{
			dictionary[item.Name.LocalName] = Value(item) ?? string.Empty;
		}
		ResolveProperties(dictionary);
		Dictionary<string, string> dictionary2 = new Dictionary<string, string>(StringComparer.Ordinal);
		if (mavenPomModel != null)
		{
			foreach (KeyValuePair<string, string> item2 in mavenPomModel.DependencyManagement)
			{
				dictionary2[item2.Key] = item2.Value;
			}
		}
		AddDependencyManagement(xElement, ns, dictionary, repositoryPath, diagnostics, visited, activeProfiles, dictionary2);
		List<MavenResolvedDependency> dependencies = ReadDependencies(xElement, ns, dictionary, dictionary2).ToList();
		AddActiveProfileDependencies(xElement, ns, dictionary, dictionary2, activeProfiles, dependencies);
				string[] modules = (from value in (xElement.Element(ns + "modules")?.Elements(ns + "module") ?? Enumerable.Empty<XElement>()).Select(Value)
			where !string.IsNullOrWhiteSpace(value)
			select value).Cast<string>().ToArray() ?? Array.Empty<string>();
				string[] licenses = (from license in (xElement.Element(ns + "licenses")?.Elements(ns + "license") ?? Enumerable.Empty<XElement>())
			select Value(license.Element(ns + "name")) into value
			where !string.IsNullOrWhiteSpace(value)
			select value).Cast<string>().ToArray() ?? Array.Empty<string>();
		return new MavenPomModel(fullPath, text, text2, text3, dictionary, dictionary2, modules, dependencies, (IReadOnlyCollection<string>)(object)licenses);
	}

	private static MavenPomModel? TryLoadParent(string path, XElement? parent, string? repositoryPath, IDiagnostics diagnostics, ISet<string> visited, IReadOnlySet<string> activeProfiles)
	{
		if (parent == null)
		{
			return null;
		}
		string text = Value(parent.Element(parent.Name.Namespace + "relativePath"));
		string path2 = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path) ?? string.Empty, string.IsNullOrEmpty(text) ? "../pom.xml" : text);
		if (File.Exists(path2))
		{
			return Load(path2, repositoryPath, diagnostics, visited, activeProfiles);
		}
		string text2 = Value(parent.Element(parent.Name.Namespace + "groupId"));
		string text3 = Value(parent.Element(parent.Name.Namespace + "artifactId"));
		string text4 = Value(parent.Element(parent.Name.Namespace + "version"));
		if (repositoryPath == null || string.IsNullOrWhiteSpace(text2) || string.IsNullOrWhiteSpace(text3) || string.IsNullOrWhiteSpace(text4))
		{
			return null;
		}
		string path3 = System.IO.Path.Combine(repositoryPath, text2.Replace('.', System.IO.Path.DirectorySeparatorChar), text3, text4, text3 + "-" + text4 + ".pom");
		return File.Exists(path3) ? Load(path3, repositoryPath, diagnostics, visited, activeProfiles) : null;
	}

	private static void AddDependencyManagement(XElement root, XNamespace ns, IReadOnlyDictionary<string, string> properties, string? repositoryPath, IDiagnostics diagnostics, ISet<string> visited, IReadOnlySet<string> activeProfiles, IDictionary<string, string> managed)
	{
		IEnumerable<XElement> enumerable = root.Element(ns + "dependencyManagement")?.Element(ns + "dependencies")?.Elements(ns + "dependency") ?? Enumerable.Empty<XElement>();
		foreach (XElement item in enumerable)
		{
			string text = Resolve(Value(item.Element(ns + "groupId")), properties);
			string text2 = Resolve(Value(item.Element(ns + "artifactId")), properties);
			string text3 = Resolve(Value(item.Element(ns + "version")), properties);
			string a = Resolve(Value(item.Element(ns + "scope")), properties);
			string text4 = Resolve(Value(item.Element(ns + "type")), properties);
			if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(text2))
			{
				continue;
			}
			if (string.Equals(a, "import", StringComparison.OrdinalIgnoreCase) && string.Equals(text4 ?? "pom", "pom", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(text3) && repositoryPath != null)
			{
				string path = System.IO.Path.Combine(repositoryPath, text.Replace('.', System.IO.Path.DirectorySeparatorChar), text2, text3, text2 + "-" + text3 + ".pom");
				if (!File.Exists(path))
				{
					continue;
				}
				MavenPomModel mavenPomModel = Load(path, repositoryPath, diagnostics, visited, activeProfiles);
				foreach (KeyValuePair<string, string> item2 in mavenPomModel.DependencyManagement)
				{
					managed[item2.Key] = item2.Value;
				}
			}
			else if (!string.IsNullOrWhiteSpace(text3))
			{
				managed[text + ":" + text2] = text3;
			}
		}
		AddActiveProfileManagement(root, ns, properties, activeProfiles, managed);
	}

	private static void AddActiveProfileManagement(XElement root, XNamespace ns, IReadOnlyDictionary<string, string> properties, IReadOnlySet<string> activeProfiles, IDictionary<string, string> managed)
	{
		XElement[] profiles = root.Element(ns + "profiles")?.Elements(ns + "profile").ToArray() ?? Array.Empty<XElement>();
		XElement[] array = profiles.Where((XElement profile) => IsProfileActive(profile, ns, properties, activeProfiles, (IReadOnlyCollection<XElement>)(object)profiles)).ToArray();
		XElement[] array2 = array;
		foreach (XElement xElement in array2)
		{
			foreach (XElement item in xElement.Element(ns + "dependencyManagement")?.Element(ns + "dependencies")?.Elements(ns + "dependency") ?? Enumerable.Empty<XElement>())
			{
				string text = Resolve(Value(item.Element(ns + "groupId")), properties);
				string text2 = Resolve(Value(item.Element(ns + "artifactId")), properties);
				string value = Resolve(Value(item.Element(ns + "version")), properties);
				if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2) && !string.IsNullOrWhiteSpace(value))
				{
					managed[text + ":" + text2] = value;
				}
			}
		}
	}

	private static IReadOnlyList<MavenResolvedDependency> ReadDependencies(XElement root, XNamespace ns, IReadOnlyDictionary<string, string> properties, IReadOnlyDictionary<string, string> managed)
	{
		List<MavenResolvedDependency> list = new List<MavenResolvedDependency>();
		foreach (XElement item in root.Element(ns + "dependencies")?.Elements(ns + "dependency") ?? Enumerable.Empty<XElement>())
		{
			string text = Resolve(Value(item.Element(ns + "groupId")), properties);
			string text2 = Resolve(Value(item.Element(ns + "artifactId")), properties);
			if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2))
			{
				string text3 = text + ":" + text2;
				string text4 = Resolve(Value(item.Element(ns + "version")), properties);
				if (string.IsNullOrWhiteSpace(text4) && managed.TryGetValue(text3, out string value))
				{
					text4 = Resolve(value, properties);
				}
				string text5 = Resolve(Value(item.Element(ns + "scope")), properties) ?? "compile";
				bool flag = string.Equals(Resolve(Value(item.Element(ns + "optional")), properties), "true", StringComparison.OrdinalIgnoreCase);
				int? num = FindElementLine(root.ToString(SaveOptions.DisableFormatting), item);
				List<MavenResolvedDependency> list2 = list;
				string name = text3;
				string version = text4 ?? string.Empty;
				string scope = text5;
				bool optional = flag;
				bool development = ((text5 == "test" || text5 == "provided") ? true : false);
				list2.Add(new MavenResolvedDependency(name, version, scope, optional, development, ReadExclusions(item, ns, properties), num, num));
			}
		}
		return list;
	}

	private static void AddActiveProfileDependencies(XElement root, XNamespace ns, IReadOnlyDictionary<string, string> properties, IReadOnlyDictionary<string, string> managed, IReadOnlySet<string> activeProfiles, List<MavenResolvedDependency> dependencies)
	{
		XElement[] profiles = root.Element(ns + "profiles")?.Elements(ns + "profile").ToArray() ?? Array.Empty<XElement>();
		foreach (XElement item in profiles.Where((XElement profile) => IsProfileActive(profile, ns, properties, activeProfiles, (IReadOnlyCollection<XElement>)(object)profiles)))
		{
			dependencies.AddRange(ReadDependencies(item, ns, properties, managed));
		}
	}

	private static bool IsProfileActive(XElement profile, XNamespace ns, IReadOnlyDictionary<string, string> properties, IReadOnlySet<string> activeProfiles, IReadOnlyCollection<XElement> container)
	{
		string text = Value(profile.Element(ns + "id"));
		if (text != null && activeProfiles.Contains(text))
		{
			return true;
		}
		XElement xElement = profile.Element(ns + "activation");
		if (xElement == null)
		{
			return false;
		}
		string text2 = Value(xElement.Element(ns + "property")?.Element(ns + "name"));
		string text3 = Value(xElement.Element(ns + "property")?.Element(ns + "value"));
		if (text2 != null)
		{
			bool flag = text2.StartsWith('!');
			string key = (flag ? text2.Substring(1) : text2);
			bool flag2 = properties.ContainsKey(key);
			bool flag3 = text3 == null || (flag2 && string.Equals(properties[key], text3, StringComparison.Ordinal));
			if (flag ? (!flag2) : flag3)
			{
				return true;
			}
		}
		string text4 = Value(xElement.Element(ns + "jdk"));
		if (text4 != null && Environment.Version.Major.ToString() == text4.TrimStart('>', '<', '='))
		{
			return true;
		}
		XElement xElement2 = xElement.Element(ns + "os");
		if (xElement2 != null && (Value(xElement2.Element(ns + "family")) == null || string.Equals(Value(xElement2.Element(ns + "family")), Environment.OSVersion.Platform.ToString(), StringComparison.OrdinalIgnoreCase)))
		{
			return true;
		}
		XElement xElement3 = xElement.Element(ns + "file");
		if (xElement3 != null)
		{
			bool flag4 = File.Exists(Value(xElement3.Element(ns + "exists")) ?? string.Empty);
			bool flag5 = File.Exists(Value(xElement3.Element(ns + "missing")) ?? string.Empty);
			if (flag4 || (!flag5 && xElement3.Element(ns + "missing") == null))
			{
				return true;
			}
		}
		return string.Equals(Value(xElement.Element(ns + "activeByDefault")), "true", StringComparison.OrdinalIgnoreCase) && !container.Any((XElement other) => other != profile && activeProfiles.Contains(Value(other.Element(ns + "id")) ?? string.Empty));
	}

	private static IReadOnlySet<string> ReadExclusions(XElement dependency, XNamespace ns, IReadOnlyDictionary<string, string> properties)
	{
		return (dependency.Element(ns + "exclusions")?.Elements(ns + "exclusion") ?? Enumerable.Empty<XElement>()).Select((XElement exclusion) => Resolve(Value(exclusion.Element(ns + "groupId")), properties) + ":" + Resolve(Value(exclusion.Element(ns + "artifactId")), properties)).ToHashSet<string>(StringComparer.Ordinal);
	}

	private static void ResolveProperties(IDictionary<string, string> properties)
	{
		string[] array = properties.Keys.ToArray();
		foreach (string key in array)
		{
			string text = properties[key];
			for (int j = 0; j < 20; j++)
			{
				int num = text.IndexOf("${", StringComparison.Ordinal);
				if (num < 0)
				{
					break;
				}
				int num2 = text.IndexOf('}', num + 2);
				if (num2 < 0)
				{
					break;
				}
				string text2 = text;
				int num3 = num + 2;
				string key2 = text2.Substring(num3, num2 - num3);
				if (!properties.TryGetValue(key2, out string value))
				{
					break;
				}
				text = text.Substring(0, num) + value + text.Substring(num2 + 1);
			}
			properties[key] = text;
		}
	}

	private static string? Resolve(string? value, IReadOnlyDictionary<string, string> properties)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		string text = value.Trim();
		for (int i = 0; i < 20; i++)
		{
			int num = text.IndexOf("${", StringComparison.Ordinal);
			if (num < 0)
			{
				return text;
			}
			int num2 = text.IndexOf('}', num + 2);
			if (num2 < 0)
			{
				return null;
			}
			string text2 = text;
			int num3 = num + 2;
			string key = text2.Substring(num3, num2 - num3);
			if (!properties.TryGetValue(key, out string value2))
			{
				return null;
			}
			text = text.Substring(0, num) + value2 + text.Substring(num2 + 1);
		}
		return text;
	}

	private static string? Value(XElement? element)
	{
		return element?.Value.Trim();
	}

	private static int? FindElementLine(string xml, XElement element)
	{
		int num = xml.IndexOf(element.ToString(SaveOptions.DisableFormatting), StringComparison.Ordinal);
		return (num < 0) ? ((int?)null) : new int?(xml.Substring(0, num).Count((char character) => character == '\n') + 1);
	}

	private static MavenPomModel Empty(string path)
	{
		return new MavenPomModel(path, string.Empty, string.Empty, string.Empty, new Dictionary<string, string>(), new Dictionary<string, string>(), Array.Empty<string>(), Array.Empty<MavenResolvedDependency>(), (IReadOnlyCollection<string>)(object)Array.Empty<string>());
	}
}
