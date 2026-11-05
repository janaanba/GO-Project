using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

	namespace OfflineCycloneDxScanner;

	public sealed class CycloneDxWriter(IClock clock, IIdentifierFactory identifierFactory) : IReportWriter
	{
		public string Write(CycloneDxReport report)
		{
			SbomDocument sbomDocument = new SbomEncoder().Encode(report);
			CycloneDocument value = new CycloneDocument("CycloneDX", "1.7", identifierFactory.CreateSerialNumber(), new CycloneMetadata(clock.UtcNow, new CycloneTools((IReadOnlyCollection<CycloneTool>)(object)new CycloneTool[1]
			{
				new CycloneTool("application", "aquasecurity", "trivy", "1.0.0", new CycloneOrganization("Aqua Security Software Ltd."))
			}), new CycloneComponent("application", sbomDocument.Root.Name, EmptyAsNull(sbomDocument.Root.Version), sbomDocument.RootReference, (IReadOnlyCollection<CycloneLicense>)(object)Array.Empty<CycloneLicense>(), (IReadOnlyCollection<CycloneProperty>)(object)new CycloneProperty[1]
			{
				new CycloneProperty("SchemaVersion", "2")
			}, (IReadOnlyCollection<CycloneHash>)(object)Array.Empty<CycloneHash>())), (IReadOnlyCollection<CycloneComponent>)(object)sbomDocument.Components.Select(component => new CycloneComponent(DisplayType(component), DisplayName(component), EmptyAsNull(component.Version), component.BomReference, (IReadOnlyCollection<CycloneLicense>)(object)component.Licenses.Select(license => new CycloneLicense(CreateLicenseChoice(license))).ToArray(), ComponentProperties(component), component.Sha256 == null ? ((IReadOnlyCollection<CycloneHash>)(object)Array.Empty<CycloneHash>()) : ((IReadOnlyCollection<CycloneHash>)(object)new CycloneHash[1]
			{
				new CycloneHash("SHA-256", component.Sha256)
			}), component.Group, component.Supplier, string.IsNullOrEmpty(component.Purl) ? null : component.Purl)).ToArray(), (IReadOnlyCollection<CycloneDependency>)(object)sbomDocument.Relationships.Select(relationship => new CycloneDependency(relationship.Source, relationship.Targets)).ToArray());
			return JsonSerializer.Serialize(value, new JsonSerializerOptions
			{
				WriteIndented = true,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				DefaultIgnoreCondition = JsonIgnoreCondition.Never,
				Converters = { (JsonConverter)new CycloneTimestampConverter() }
			});
		}

		private static CycloneLicenseChoice CreateLicenseChoice(string license)
		{
			if (Uri.TryCreate(license, UriKind.Absolute, out Uri result) && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
			{
				return new CycloneLicenseChoice(null, null, license);
			}
			if (license.Contains(" AND ", StringComparison.OrdinalIgnoreCase) || license.Contains(" OR ", StringComparison.OrdinalIgnoreCase))
			{
				return new CycloneLicenseChoice(null, null, null, license);
			}
			return new CycloneLicenseChoice(license);
		}

		private static string? EmptyAsNull(string value)
		{
			return string.IsNullOrEmpty(value) ? null : value;
		}

		private static string DisplayType(SbomComponent component)
		{
			return component.IsInputFile ? "application" : component.Type;
		}

		private static string DisplayName(SbomComponent component)
		{
			if (component.IsInputFile || component.Ecosystem is not ("gradle" or "maven"))
			{
				return component.Name;
			}

			int separator = component.Name.IndexOf(':');
			return separator >= 0 ? component.Name[(separator + 1)..] : component.Name;
		}

		private static IReadOnlyCollection<CycloneProperty> ComponentProperties(SbomComponent component)
		{
			if (component.IsInputFile)
			{
				return new CycloneProperty[2]
				{
					new CycloneProperty("aquasecurity:trivy:Class", "lang-pkgs"),
					new CycloneProperty("aquasecurity:trivy:Type", component.Ecosystem == "maven" ? "pom" : component.Ecosystem)
				};
			}

			List<CycloneProperty> list = new List<CycloneProperty>
			{
				new CycloneProperty("aquasecurity:trivy:PkgID", PackageId(component)),
				new CycloneProperty("aquasecurity:trivy:PkgType", PackageType(component)),
				new CycloneProperty("FilePath", component.SourceFile)
			};
			if (component.IsRoot)
			{
				list.Add(new CycloneProperty("Root", "true"));
			}
			if (component.IsWorkspace)
			{
				list.Add(new CycloneProperty("Workspace", "true"));
			}
			if (component.SourceName != null)
			{
				list.Add(new CycloneProperty("SourceName", component.SourceName));
			}
			if (component.SourceVersion != null)
			{
				list.Add(new CycloneProperty("SourceVersion", component.SourceVersion));
			}
			if (component.StartLine.HasValue)
			{
				list.Add(new CycloneProperty("StartLine", component.StartLine.Value.ToString(CultureInfo.InvariantCulture)));
				list.Add(new CycloneProperty("EndLine", (component.EndLine ?? component.StartLine).Value.ToString(CultureInfo.InvariantCulture)));
			}
			if (component.Files != null)
			{
				list.AddRange(component.Files.Select(file => new CycloneProperty("File", file)));
			}
			if (component.Qualifiers != null)
			{
				list.AddRange(from pair in component.Qualifiers.OrderBy(pair => pair.Key, StringComparer.Ordinal)
					select new CycloneProperty("Qualifier:" + pair.Key, pair.Value));
			}
			if (component.Properties != null)
			{
				list.AddRange(from pair in component.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal)
					select new CycloneProperty(pair.Key, pair.Value));
			}
			if (component.Vulnerabilities != null)
			{
				list.AddRange(component.Vulnerabilities.Select(vulnerability => new CycloneProperty("Vulnerability", vulnerability)));
			}
			return list;
		}

		private static string PackageType(SbomComponent component)
		{
			return component.Ecosystem == "maven" ? "pom" : component.Ecosystem;
		}

		private static string PackageId(SbomComponent component)
		{
			return component.Ecosystem switch
			{
				"composer" or "pub" => $"{component.Name}@{component.Version}",
				"conan" => $"{component.Name}/{component.Version}",
				"maven" => MavenPackageId(component),
				"gradle" => $"{component.Name}:{component.Version}",
				_ => component.Purl
			};
		}

		private static string MavenPackageId(SbomComponent component)
		{
			string gav = $"{component.Name}:{component.Version}";
			string path = Path.GetFullPath(component.SourceFile).Replace(Path.DirectorySeparatorChar, '/');
			ulong hash = HashFinish(HashUnordered(HashOrdered(HashString("gav"), HashString(gav)), HashOrdered(HashString("path"), HashString(path))));
			return $"{gav}::{hash:x16}"[..(gav.Length + 10)];
		}

		private static ulong HashString(string value) => HashBytes(Encoding.UTF8.GetBytes(value));
		private static ulong HashUnordered(ulong first, ulong second) => first ^ second;
		private static ulong HashFinish(ulong value) => HashBytes(BitConverter.GetBytes(value));

		private static ulong HashOrdered(ulong first, ulong second)
		{
			byte[] bytes = new byte[16];
			Array.Copy(BitConverter.GetBytes(first), 0, bytes, 0, 8);
			Array.Copy(BitConverter.GetBytes(second), 0, bytes, 8, 8);
			return HashBytes(bytes);
		}

		private static ulong HashBytes(byte[] bytes)
		{
			const ulong offset = 14695981039346656037;
			const ulong prime = 1099511628211;
			ulong hash = offset;
			foreach (byte item in bytes) hash = (hash * prime) ^ item;
			return hash;
		}
	}
/*

public sealed class CycloneDxWriter(IClock clock, IIdentifierFactory identifierFactory) : IReportWriter
{
	public string Write(CycloneDxReport report)
	{
		SbomDocument sbomDocument = new SbomEncoder().Encode(report);
		CycloneDocument value = new CycloneDocument("CycloneDX", "1.7", identifierFactory.CreateSerialNumber(), new CycloneMetadata(clock.UtcNow, new CycloneTools((IReadOnlyCollection<CycloneTool>)(object)new CycloneTool[1]
		{
			new CycloneTool("application", "aquasecurity", "trivy", "1.0.0", new CycloneOrganization("Aqua Security Software Ltd."))
		}), new CycloneComponent("application", sbomDocument.Root.Name, sbomDocument.Root.Version, sbomDocument.RootReference, (IReadOnlyCollection<CycloneLicense>)(object)Array.Empty<CycloneLicense>(), (IReadOnlyCollection<CycloneProperty>)(object)new CycloneProperty[1]
		{
			new CycloneProperty("SchemaVersion", "2")
		}, (IReadOnlyCollection<CycloneHash>)(object)Array.Empty<CycloneHash>())), (IReadOnlyCollection<CycloneComponent>)(object)sbomDocument.Components.Select((SbomComponent component) => new CycloneComponent(component.Type, component.Name, component.Version, component.BomReference, (IReadOnlyCollection<CycloneLicense>)(object)component.Licenses.Select((string license) => new CycloneLicense(CreateLicenseChoice(license))).ToArray(), ComponentProperties(component), (component.Sha256 == null) ? ((IReadOnlyCollection<CycloneHash>)(object)Array.Empty<CycloneHash>()) : ((IReadOnlyCollection<CycloneHash>)(object)new CycloneHash[1]
		{
			new CycloneHash("SHA-256", component.Sha256)
		}), component.Group, component.Supplier, string.IsNullOrEmpty(component.Purl) ? null : component.Purl)).ToArray(), (IReadOnlyCollection<CycloneDependency>)(object)sbomDocument.Relationships.Select((SbomRelationship relationship) => new CycloneDependency(relationship.Source, relationship.Targets)).ToArray());
		return JsonSerializer.Serialize(value, new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.Never,
			Converters = { (JsonConverter)new CycloneTimestampConverter() }
		});
	}

	private static CycloneLicenseChoice CreateLicenseChoice(string license)
	{
		if (Uri.TryCreate(license, UriKind.Absolute, out Uri result) && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
		{
			return new CycloneLicenseChoice(null, null, license);
		}
		if (license.Contains(" AND ", StringComparison.OrdinalIgnoreCase) || license.Contains(" OR ", StringComparison.OrdinalIgnoreCase))
		{
			return new CycloneLicenseChoice(null, null, null, license);
		}
		return new CycloneLicenseChoice(license);
	}

	private static IReadOnlyCollection<CycloneProperty> ComponentProperties(SbomComponent component)
	{
		if (component.IsInputFile)
		{
			return new CycloneProperty[2]
			{
				new CycloneProperty("aquasecurity:trivy:Class", "lang-pkgs"),
				new CycloneProperty("aquasecurity:trivy:Type", component.Ecosystem)
			};
		}
		List<CycloneProperty> list = new List<CycloneProperty>
		{
			new CycloneProperty("PkgID", component.Purl),
			new CycloneProperty("PkgType", component.Ecosystem),
			new CycloneProperty("FilePath", component.SourceFile)
		};
		if (component.IsRoot)
		{
			list.Add(new CycloneProperty("Root", "true"));
		}
		if (component.IsWorkspace)
		{
			list.Add(new CycloneProperty("Workspace", "true"));
		}
		if (component.Relationship != null)
		{
			list.Add(new CycloneProperty("Relationship", component.Relationship));
		}
		if (component.SourceName != null)
		{
			list.Add(new CycloneProperty("SourceName", component.SourceName));
		}
		if (component.SourceVersion != null)
		{
			list.Add(new CycloneProperty("SourceVersion", component.SourceVersion));
		}
		if (component.StartLine.HasValue)
		{
			list.Add(new CycloneProperty("StartLine", component.StartLine.Value.ToString(CultureInfo.InvariantCulture)));
			list.Add(new CycloneProperty("EndLine", (component.EndLine ?? component.StartLine).Value.ToString(CultureInfo.InvariantCulture)));
		}
		if (component.Files != null)
		{
			list.AddRange(component.Files.Select((string file) => new CycloneProperty("File", file)));
		}
		if (component.Qualifiers != null)
		{
			list.AddRange(from pair in component.Qualifiers.OrderBy<KeyValuePair<string, string>, string>((KeyValuePair<string, string> pair) => pair.Key, StringComparer.Ordinal)
				select new CycloneProperty("Qualifier:" + pair.Key, pair.Value));
		}
		if (component.Properties != null)
		{
			list.AddRange(from pair in component.Properties.OrderBy<KeyValuePair<string, string>, string>((KeyValuePair<string, string> pair) => pair.Key, StringComparer.Ordinal)
				select new CycloneProperty(pair.Key, pair.Value));
		}
		if (component.Vulnerabilities != null)
		{
			list.AddRange(component.Vulnerabilities.Select((string vulnerability) => new CycloneProperty("Vulnerability", vulnerability)));
		}
		return list;
	}
}
*/
