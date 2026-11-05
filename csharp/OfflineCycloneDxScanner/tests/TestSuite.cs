using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OfflineCycloneDxScanner;
using YamlDotNet.Core;

internal static class TestSuite
{
	private sealed class TestProject(string root) : IDisposable
	{
		public string Root { get; } = root;

		public LocalFileSystem FileSystem { get; } = new LocalFileSystem();

		public static TestProject Create(params (string Name, string Content)[] files)
		{
			TestProject testProject = new TestProject(Path.Combine(Path.GetTempPath(), $"offline-cyclonedx-tests-{Guid.NewGuid():N}"));
			Directory.CreateDirectory(testProject.Root);
			for (int i = 0; i < files.Length; i++)
			{
				(string, string) tuple = files[i];
				string path = Path.Combine(testProject.Root, tuple.Item1);
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, tuple.Item2);
			}
			return testProject;
		}

		public void Dispose()
		{
			Directory.Delete(Root, recursive: true);
		}
	}

	private sealed class TestDiagnostics : IDiagnostics
	{
		public List<string> InfoMessages { get; } = new List<string>();

		public void Info(string message)
		{
			InfoMessages.Add(message);
		}

		public void Warning(string message)
		{
		}

		public void Error(string message)
		{
		}
	}

	private sealed class FixedClock : IClock
	{
		public DateTimeOffset UtcNow => new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);
	}

	private sealed class FixedIdentifierFactory : IIdentifierFactory
	{
		public string CreateSerialNumber()
		{
			return "urn:uuid:test";
		}
	}

	private static class Assert
	{
		public static void True(bool condition, string message)
		{
			if (!condition)
			{
				throw new InvalidOperationException(message);
			}
		}

		public static void Equal<T>(T expected, T actual, string message)
		{
			if (!EqualityComparer<T>.Default.Equals(expected, actual))
			{
				throw new InvalidOperationException($"{message}; expected {expected}, actual {actual}");
			}
		}

		public static void Contains<T>(IEnumerable<T> values, Func<T, bool> predicate, string message)
		{
			if (!values.Any(predicate))
			{
				throw new InvalidOperationException(message);
			}
		}

		public static void Throws<T>(Action action, string message) where T : Exception
		{
			try
			{
				action();
			}
			catch (T)
			{
				return;
			}
			throw new InvalidOperationException(message);
		}
	}

	public static void RunAll()
	{
		Run("Conan v1 graph parsing", ConanGraphParsing);
		Run("Conan v2 requirements parsing", ConanRequirementsParsing);
		Run("Dart lock parsing", DartLockParsing);
		Run("Dart SDK version resolution", DartSdkVersionResolution);
		Run("Dart unsupported SDK constraints", DartUnsupportedSdkConstraints);
		Run("Dart duplicate YAML keys", DartDuplicateYamlKeys);
		Run("Composer packages and platform filtering", ComposerParsing);
		Run("Composer production package precedence", ComposerProductionPackagePrecedence);
		Run("Composer license and platform semantics", ComposerLicenseAndPlatformSemantics);
		Run("License expression parsing", LicenseExpressionParsing);
		Run("Canonical PURL formatting", CanonicalPurlFormatting);
		Run("Gradle development classification", GradleParsing);
		Run("Gradle empty configuration classification", GradleEmptyConfiguration);
		Run("Gradle duplicate package semantics", GradleDuplicatePackageSemantics);
		Run("Gradle cached POM enrichment", GradleCachedPomEnrichment);
		Run("Maven properties and scopes", MavenParsing);
		Run("Maven local repository enrichment", MavenLocalEnrichment);
		Run("Maven deep dependency resolution", MavenDeepDependencyResolution);
		Run("Maven parent dependency management", MavenParentDependencyManagement);
		Run("Maven BOM and profile management", MavenBomAndProfileManagement);
		Run("Maven nearest dependency mediation", MavenNearestDependencyMediation);
		Run("Maven root and module relationships", MavenRootAndModuleRelationships);
		Run("Maven repository path resolution", MavenRepositoryPathResolution);
		Run("Development dependency filtering", DevelopmentFiltering);
		Run("CLI option parsing", CliOptionParsing);
		Run("CLI validation", CliValidation);
		Run("Duplicate package instances", DuplicatePackageInstances);
		Run("SBOM identity and metadata", SbomIdentityAndMetadata);
		Run("CycloneDX serialization", CycloneDxSerialization);
		Run("Offline scanner discovery", OfflineScannerDiscovery);
		Run("Offline scanner file target", OfflineScannerFileTarget);
		Run("Multiple lockfiles duplicate package", MultipleLockfilesDuplicatePackage);
		Run("Maven exclusion path mediation", MavenExclusionPathMediation);
		Run("Maven module and license metadata", MavenModuleAndLicenseMetadata);
		Run("Maven settings active profile", MavenSettingsActiveProfile);
		Run("Dart compound SDK constraints", DartCompoundSdkConstraints);
	}

	private static void Run(string name, Action test)
	{
		test();
		Console.WriteLine("PASS " + name);
	}

	private static void ConanGraphParsing()
	{
		using TestProject testProject = TestProject.Create(("conan.lock", "{\"graph_lock\":{\"nodes\":{\"0\":{\"ref\":\"\",\"requires\":[\"1\"]},\"1\":{\"ref\":\"zlib/1.2.13\",\"requires\":[]},\"2\":{\"ref\":\"openssl/3.0.5@user/stable\",\"requires\":[]}}}}"));
		DependencyGraph dependencyGraph = Analyze(new ConanAnalyzer(testProject.FileSystem), testProject);
		Assert.Equal(2, dependencyGraph.Components.Count, "Conan package count");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "openssl" && component.Version == "3.0.5" && component.Relationship == "indirect", "Conan reference suffix parsing");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "zlib" && component.Relationship == "direct", "Conan direct relationship");
		Assert.True(dependencyGraph.Components.All((DependencyComponent component) => component.Name != ""), "Conan root node excluded");
		Assert.True(dependencyGraph.Components.All((DependencyComponent component) => component.StartLine.HasValue), "Conan source locations");
	}

	private static void ConanRequirementsParsing()
	{
		using TestProject testProject = TestProject.Create(("conan.lock", "{\"requires\":[\"fmt/10.2.1\",\"boost/1.83.0@user/testing#revision\"]}"));
		Assert.Equal(2, Analyze(new ConanAnalyzer(testProject.FileSystem), testProject).Components.Count, "Conan v2 package count");
	}

	private static void DartLockParsing()
	{
		using TestProject testProject = TestProject.Create(("pubspec.lock", "packages:\n  http:\n    dependency: \"direct main\"\n    version: \"1.2.0\"\n  test:\n    dependency: \"direct dev\"\n    version: \"1.25.0\"\n  meta:\n    dependency: transitive\n    version: \"1.11.0\"\n"));
		DependencyGraph dependencyGraph = Analyze(new DartAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
		Assert.Equal(3, dependencyGraph.Components.Count, "Dart package count");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "test" && component.Development, "Dart development dependency retained");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "http" && component.Relationship == "direct", "Dart direct relationship");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "meta" && component.Relationship == "indirect", "Dart indirect relationship");
	}

	private static void DartSdkVersionResolution()
	{
		using TestProject testProject = TestProject.Create(("pubspec.lock", "packages:\n  sdk_package:\n    dependency: transitive\n    source: sdk\n    version: \"0.0.0\"\n    description: dart\nsdks:\n  dart: \">=3.2.0 <4.0.0\"\n"));
		DependencyGraph dependencyGraph = Analyze(new DartAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "sdk_package" && component.Version == "3.2.0", "Dart SDK minimum version");
	}

	private static void DartUnsupportedSdkConstraints()
	{
		using TestProject testProject = TestProject.Create(("pubspec.lock", "packages:\n  sdk_package:\n    dependency: transitive\n    source: sdk\n    version: \"0.0.0\"\n    description: dart\nsdks:\n  dart: \" >3.2.0 <4.0.0\"\n"));
		DependencyGraph dependencyGraph = Analyze(new DartAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "sdk_package" && component.Version == "0.0.0", "unsupported Dart SDK constraint fallback");
	}

	private static void DartDuplicateYamlKeys()
	{
		TestProject project = TestProject.Create(("pubspec.lock", "packages:\n  http:\n    version: 1.0.0\n    version: 2.0.0\n"));
		try
		{
			Assert.Throws<YamlException>(delegate
			{
				Analyze(new DartAnalyzer(project.FileSystem), project, includeDevDependencies: true);
			}, "duplicate Dart YAML key");
		}
		finally
		{
			if (project != null)
			{
				((IDisposable)project).Dispose();
			}
		}
	}

	private static void ComposerParsing()
	{
		using TestProject testProject = TestProject.Create(("composer.lock", "{\"packages\":[{\"name\":\"vendor/app\",\"version\":\"1.0.0\",\"license\":[\"MIT or Apache-2.0\"],\"require\":{\"vendor/lib\":\"^2.0\",\"php\":\">=8.0\",\"ext-json\":\"*\",\"extcustom\":\"*\"}},{\"name\":\"vendor/lib\",\"version\":\"2.1.0\",\"require\":{}}],\"packages-dev\":[{\"name\":\"vendor/test\",\"version\":\"1.0.0\",\"require\":{}}]}"));
		DependencyGraph dependencyGraph = Analyze(new ComposerAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
		Assert.Equal(3, dependencyGraph.Components.Count, "Composer package count");
		Assert.True(dependencyGraph.Edges["pkg:composer/vendor/app@1.0.0"].SetEquals(new string[1] { "pkg:composer/vendor/lib@2.1.0" }), "Composer platform filtering");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Licenses.Contains("MIT"), "Composer license");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Licenses.Contains("Apache-2.0"), "Composer compound license");
	}

	private static void LicenseExpressionParsing()
	{
		IReadOnlyCollection<string> first = LicenseExpressionParser.Parse(new string[2] { "MIT OR Apache-2.0", "GPL-2.0-only AND BSD-3-Clause" });
		Assert.True(first.SequenceEqual(new string[4] { "MIT", "Apache-2.0", "GPL-2.0-only", "BSD-3-Clause" }), "license expression tokens");
	}

	private static void CanonicalPurlFormatting()
	{
		Assert.Equal("pkg:maven/org.example/artifact%2Fname@1.0%2Bbuild", PackageHelper.Purl("MAVEN", "org.example:artifact/name", "1.0+build"), "Maven namespace and reserved encoding");
		Assert.Equal("pkg:maven/org.example/library@1.0", PackageHelper.Purl("Gradle", "org.example/library", "1.0"), "Gradle Maven identity normalization");
		Assert.Equal("pkg:composer/vendor/package@1.0", PackageHelper.ComposerPurl("Vendor/Package", "1.0"), "Composer canonical casing");
		Assert.Equal("pkg:pub/caf%C3%A9@1.0", PackageHelper.Purl("pub", "cafe\u0301", "1.0"), "UTF-8 NFC encoding");
		Assert.Equal("pkg:pub/library@1.0?arch=arm64&source=https:%2F%2Fexample.test", PackageHelper.Purl("pub", "library", "1.0", new Dictionary<string, string>
		{
			["source"] = "https://example.test",
			["arch"] = "arm64"
		}), "qualifier ordering and encoding");
		Assert.Equal("pkg:pub/library@1.0#src/main", PackageHelper.Purl("pub", "library", "1.0", null, "src/main"), "subpath encoding");
		Assert.Throws<ArgumentException>(delegate
		{
			PackageHelper.Purl("pub", string.Empty, "1.0");
		}, "invalid PURL input");
	}

	private static void ComposerProductionPackagePrecedence()
	{
		using TestProject testProject = TestProject.Create(("composer.lock", "{\"packages\":[{\"name\":\"vendor/app\",\"version\":\"1.0.0\",\"require\":{\"vendor/shared\":\"^1.0\"}},{\"name\":\"vendor/shared\",\"version\":\"1.2.0\",\"require\":{}}],\"packages-dev\":[{\"name\":\"vendor/shared\",\"version\":\"2.0.0\",\"require\":{}}]}"));
		DependencyGraph dependencyGraph = Analyze(new ComposerAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "vendor/shared" && component.Version == "1.2.0", "Composer production package retained");
		Assert.True(!dependencyGraph.Components.Any((DependencyComponent component) => component.Name == "vendor/shared" && component.Version == "2.0.0"), "Composer duplicate dev package skipped");
		Assert.True(dependencyGraph.Edges["pkg:composer/vendor/app@1.0.0"].SetEquals(new string[1] { "pkg:composer/vendor/shared@1.2.0" }), "Composer production package edge");
	}

	private static void ComposerLicenseAndPlatformSemantics()
	{
		using TestProject testProject = TestProject.Create(("composer.lock", "{\"packages\":[{\"name\":\"vendor/app\",\"version\":\"1.0.0\",\"license\":\"MIT OR Apache-2.0\",\"require\":{\"vendor/lib\":\"^1\",\"lib-z\":\"*\",\"composer-runtime-api\":\"*\"}},{\"name\":\"vendor/lib\",\"version\":\"1.2.0\",\"require\":{}}],\"packages-dev\":[]}"));
		DependencyGraph dependencyGraph = Analyze(new ComposerAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
		DependencyComponent dependencyComponent = dependencyGraph.Components.Single((DependencyComponent component) => component.Name == "vendor/app");
		Assert.True(dependencyComponent.Licenses.SequenceEqual(new string[2] { "MIT", "Apache-2.0" }), "Composer license separators");
		Assert.True(dependencyComponent.StartLine.HasValue, "Composer source location");
		Assert.True(dependencyGraph.Edges[dependencyComponent.Purl].SetEquals(new string[1] { "pkg:composer/vendor/lib@1.2.0" }), "Composer platform package filtering");
		string json = new CycloneDxWriter(new FixedClock(), new FixedIdentifierFactory()).Write(new CycloneDxReport(testProject.Root, dependencyGraph));
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		Assert.Equal<string>("MIT", jsonDocument.RootElement.GetProperty("components").EnumerateArray().Single((JsonElement component) => component.GetProperty("name").GetString() == "vendor/app")
			.GetProperty("licenses")[0].GetProperty("license").GetProperty("id").GetString(), "CycloneDX license ID");
	}

	private static void GradleParsing()
	{
		using TestProject testProject = TestProject.Create(("gradle.lockfile", "org.example:runtime:1.0=compileClasspath\norg.example:test-lib:2.0=testCompileClasspath,testRuntimeClasspath\norg.example:case-sensitive:3.0=TestCompileClasspath\n"));
		DependencyGraph dependencyGraph = Analyze(new GradleAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
		Assert.Equal(3, dependencyGraph.Components.Count, "Gradle package count");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:test-lib" && component.Development, "Gradle test classification");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:case-sensitive" && !component.Development, "Gradle configuration case sensitivity");
	}

	private static void GradleEmptyConfiguration()
	{
		using TestProject testProject = TestProject.Create(("gradle.lockfile", "org.example:runtime:1.0=\norg.example:runtime-no-config:1.1\n"));
		DependencyGraph dependencyGraph = Analyze(new GradleAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:runtime" && !component.Development, "Gradle empty configuration classification");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:runtime-no-config" && !component.Development, "Gradle missing configuration classification");
	}

	private static void GradleDuplicatePackageSemantics()
	{
		using TestProject testProject = TestProject.Create(("gradle.lockfile", "org.example:shared:1.0=testRuntimeClasspath\norg.example:shared:1.0=compileClasspath\norg.example:shared:2.0=compileClasspath\norg.example:whitespace:3.0= testRuntimeClasspath\n"));
		DependencyGraph dependencyGraph = Analyze(new GradleAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
		Assert.Equal(3, dependencyGraph.Components.Count, "Gradle duplicate package count");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:shared" && component.Version == "1.0" && !component.Development, "Gradle production duplicate wins");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:shared" && component.Version == "2.0", "Gradle distinct locked version retained");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:whitespace" && !component.Development, "Gradle configuration whitespace semantics");
	}

	private static void GradleCachedPomEnrichment()
	{
		using TestProject testProject = TestProject.Create(("gradle.lockfile", "org.example:runtime:1.0=compileClasspath\norg.example:child:2.0=compileClasspath\n"));
		string? previousGradleHome = Environment.GetEnvironmentVariable("GRADLE_USER_HOME");
		string gradleHome = Path.Combine(testProject.Root, "gradle-home");
		string cacheDirectory = Path.Combine(gradleHome, "caches", "modules-2", "files-2.1", "org.example", "runtime", "1.0", "runtime-hash");
		Directory.CreateDirectory(cacheDirectory);
		File.WriteAllText(Path.Combine(cacheDirectory, "runtime-1.0.pom"), "<project><licenses><license><name>Apache-2.0</name></license></licenses><dependencies><dependency><groupId>org.example</groupId><artifactId>child</artifactId><version>2.0</version></dependency></dependencies></project>");
		try
		{
			Environment.SetEnvironmentVariable("GRADLE_USER_HOME", gradleHome);
			DependencyGraph dependencyGraph = Analyze(new GradleAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
			DependencyComponent runtime = dependencyGraph.Components.Single((DependencyComponent component) => component.Name == "org.example:runtime");
			Assert.True(runtime.Licenses.SequenceEqual(new[] { "Apache-2.0" }), "Gradle cached POM license");
			Assert.True(dependencyGraph.Edges[runtime.Purl].SetEquals(new[] { "pkg:maven/org.example/child@2.0" }), "Gradle cached POM edge");
		}
		finally
		{
			Environment.SetEnvironmentVariable("GRADLE_USER_HOME", previousGradleHome);
		}
	}

	private static void MavenParsing()
	{
		using TestProject testProject = TestProject.Create(("pom.xml", "<project><properties><library.version>1.4.0</library.version></properties><dependencies><dependency><groupId>org.example</groupId><artifactId>runtime</artifactId><version>${library.version}</version></dependency><dependency><groupId>org.example</groupId><artifactId>test-lib</artifactId><version>2.0</version><scope>test</scope></dependency></dependencies></project>"));
		DependencyGraph dependencyGraph = Analyze(new MavenAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Purl == "pkg:maven/org.example/runtime@1.4.0", "Maven property resolution");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:test-lib" && component.Development, "Maven test scope");
	}

	private static void MavenLocalEnrichment()
	{
		using TestProject testProject = TestProject.Create(("pom.xml", "<project><dependencies><dependency><groupId>org.example</groupId><artifactId>runtime</artifactId><version>1.0</version></dependency></dependencies></project>"));
		string text = Path.Combine(testProject.Root, "m2", "org", "example", "runtime", "1.0");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "runtime-1.0.pom"), "<project><dependencies><dependency><groupId>org.example</groupId><artifactId>child</artifactId><version>2.0</version></dependency></dependencies></project>");
		DependencyGraph dependencyGraph = Analyze(new MavenAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true, Path.Combine(testProject.Root, "m2"));
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:child", "Maven local component");
		Assert.True(dependencyGraph.Edges["pkg:maven/org.example/runtime@1.0"].Contains("pkg:maven/org.example/child@2.0"), "Maven local edge");
	}

	private static void MavenDeepDependencyResolution()
	{
		using TestProject testProject = TestProject.Create(("pom.xml", "<project><dependencies><dependency><groupId>org.example</groupId><artifactId>runtime</artifactId><version>1.0</version></dependency></dependencies></project>"));
		CreateMavenPom(testProject.Root, "org.example", "runtime", "1.0", "<dependencies><dependency><groupId>org.example</groupId><artifactId>child</artifactId><version>2.0</version><exclusions><exclusion><groupId>org.example</groupId><artifactId>excluded</artifactId></exclusion></exclusions></dependency><dependency><groupId>org.example</groupId><artifactId>test-only</artifactId><version>1.0</version><scope>test</scope></dependency></dependencies>");
		CreateMavenPom(testProject.Root, "org.example", "child", "2.0", "<dependencies><dependency><groupId>org.example</groupId><artifactId>grandchild</artifactId><version>3.0</version></dependency><dependency><groupId>org.example</groupId><artifactId>excluded</artifactId><version>4.0</version></dependency></dependencies>");
		CreateMavenPom(testProject.Root, "org.example", "grandchild", "3.0", "<dependencies><dependency><groupId>org.example</groupId><artifactId>runtime</artifactId><version>1.0</version></dependency></dependencies>");
		DependencyGraph dependencyGraph = Analyze(new MavenAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true, Path.Combine(testProject.Root, "m2"));
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:grandchild", "Maven nested component");
		Assert.True(!dependencyGraph.Components.Any((DependencyComponent component) => component.Name == "org.example:excluded"), "Maven exclusion");
		Assert.True(!dependencyGraph.Components.Any((DependencyComponent component) => component.Name == "org.example:test-only"), "Maven test scope filtering");
		Assert.True(dependencyGraph.Edges["pkg:maven/org.example/grandchild@3.0"].Contains("pkg:maven/org.example/runtime@1.0"), "Maven cycle protection");
	}

	private static void MavenParentDependencyManagement()
	{
		using TestProject testProject = TestProject.Create(("pom.xml", "<project><dependencies><dependency><groupId>org.example</groupId><artifactId>runtime</artifactId><version>1.0</version></dependency></dependencies></project>"));
		CreateMavenPom(testProject.Root, "org.example", "runtime", "1.0", "<parent><groupId>org.example</groupId><artifactId>parent</artifactId><version>1.0</version><relativePath>parent.pom</relativePath></parent><dependencies><dependency><groupId>org.example</groupId><artifactId>child</artifactId><version>${child.version}</version></dependency></dependencies>");
		string path = Path.Combine(testProject.Root, "m2", "org", "example", "runtime", "1.0");
		File.WriteAllText(Path.Combine(path, "parent.pom"), "<project><properties><child.version>2.0</child.version></properties></project>");
		DependencyGraph dependencyGraph = Analyze(new MavenAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true, Path.Combine(testProject.Root, "m2"));
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:child" && component.Version == "2.0", "Maven parent property inheritance");
	}

	private static void MavenBomAndProfileManagement()
	{
		using TestProject testProject = TestProject.Create(("pom.xml", "<project><dependencyManagement><dependencies><dependency><groupId>org.example</groupId><artifactId>example-bom</artifactId><version>1.0</version><type>pom</type><scope>import</scope></dependency></dependencies></dependencyManagement><profiles><profile><activation><activeByDefault>true</activeByDefault></activation><dependencyManagement><dependencies><dependency><groupId>org.example</groupId><artifactId>profile-lib</artifactId><version>3.0</version></dependency></dependencies></dependencyManagement></profile></profiles><dependencies><dependency><groupId>org.example</groupId><artifactId>bom-lib</artifactId></dependency><dependency><groupId>org.example</groupId><artifactId>profile-lib</artifactId></dependency></dependencies></project>"));
		CreateMavenPom(testProject.Root, "org.example", "example-bom", "1.0", "<dependencyManagement><dependencies><dependency><groupId>org.example</groupId><artifactId>bom-lib</artifactId><version>2.0</version></dependency></dependencies></dependencyManagement>");
		DependencyGraph dependencyGraph = Analyze(new MavenAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true, Path.Combine(testProject.Root, "m2"));
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:bom-lib" && component.Version == "2.0", "Maven imported BOM version");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:profile-lib" && component.Version == "3.0", "Maven active profile version");
	}

	private static void MavenNearestDependencyMediation()
	{
		using TestProject testProject = TestProject.Create(("pom.xml", "<project><dependencies><dependency><groupId>org.example</groupId><artifactId>runtime</artifactId><version>1.0</version></dependency><dependency><groupId>org.example</groupId><artifactId>child</artifactId><version>2.0</version></dependency></dependencies></project>"));
		CreateMavenPom(testProject.Root, "org.example", "child", "2.0", "<dependencies><dependency><groupId>org.example</groupId><artifactId>runtime</artifactId><version>9.0</version></dependency></dependencies>");
		DependencyGraph dependencyGraph = Analyze(new MavenAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true, Path.Combine(testProject.Root, "m2"));
		Assert.True(dependencyGraph.Components.Any((DependencyComponent component) => component.Purl == "pkg:maven/org.example/runtime@1.0"), "direct Maven dependency wins mediation");
		Assert.True(!dependencyGraph.Components.Any((DependencyComponent component) => component.Purl == "pkg:maven/org.example/runtime@9.0"), "transitive Maven conflict is mediated");
		Assert.True(dependencyGraph.Edges["pkg:maven/org.example/child@2.0"].Contains("pkg:maven/org.example/runtime@1.0"), "mediated Maven edge");
	}

	private static void MavenRepositoryPathResolution()
	{
		using TestProject testProject = TestProject.Create();
		string text = Path.Combine(testProject.Root, "custom-repository");
		string text2 = Path.Combine(testProject.Root, ".m2");
		Directory.CreateDirectory(text2);
		File.WriteAllText(Path.Combine(text2, "settings.xml"), "<settings><localRepository>" + text + "</localRepository></settings>");
		Assert.Equal(text, MavenLocalRepositoryReader.ResolveRepositoryPath(null, testProject.Root), "Maven settings repository path");
		Assert.Equal(text, MavenLocalRepositoryReader.ResolveRepositoryPath(text, testProject.Root), "Maven configured repository path");
	}

	private static void MavenRootAndModuleRelationships()
	{
		using TestProject testProject = TestProject.Create(("pom.xml", "<project><groupId>org.example</groupId><artifactId>parent</artifactId><version>1.0</version><modules><module>module-a</module></modules></project>"), (Path.Combine("module-a", "pom.xml"), "<project><parent><groupId>org.example</groupId><artifactId>parent</artifactId><version>1.0</version></parent><artifactId>module-a</artifactId><dependencies><dependency><groupId>org.example</groupId><artifactId>library</artifactId><version>2.0</version></dependency></dependencies></project>"));
		DependencyGraph dependencyGraph = Analyze(new MavenAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
		Assert.True(dependencyGraph.Edges["pkg:maven/org.example/parent@1.0"].Contains("pkg:maven/org.example/module-a@1.0"), "Maven root to module relationship");
		Assert.True(dependencyGraph.Edges["pkg:maven/org.example/module-a@1.0"].Contains("pkg:maven/org.example/library@2.0"), "Maven module to direct dependency relationship");
	}

	private static void CreateMavenPom(string root, string group, string artifact, string version, string dependencies)
	{
		string text = Path.Combine(root, "m2", group.Replace('.', Path.DirectorySeparatorChar), artifact, version);
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, artifact + "-" + version + ".pom"), $"<project><groupId>{group}</groupId><artifactId>{artifact}</artifactId><version>{version}</version>{dependencies}</project>");
	}

	private static void DevelopmentFiltering()
	{
		using TestProject testProject = TestProject.Create(("gradle.lockfile", "org.example:runtime:1.0=compileClasspath\norg.example:test-lib:2.0=testCompileClasspath\n"));
		IProjectAnalyzer[] analyzers = new IProjectAnalyzer[1]
		{
			new GradleAnalyzer(testProject.FileSystem)
		};
		OfflineScanner offlineScanner = new OfflineScanner(analyzers, testProject.FileSystem, new TestDiagnostics());
		Assert.Equal(1, offlineScanner.Scan(new ScanOptions(testProject.Root, null, Offline: true, IncludeDevDependencies: false, null)).Graph.Components.Count, "development dependencies excluded by default");
		Assert.Equal(2, offlineScanner.Scan(new ScanOptions(testProject.Root, null, Offline: true, IncludeDevDependencies: true, null)).Graph.Components.Count, "development dependencies included");
	}

	private static void CycloneDxSerialization()
	{
		string tempFileName = Path.GetTempFileName();
		File.WriteAllText(tempFileName, "local evidence");
		DependencyGraph dependencyGraph = new DependencyGraph();
		try
		{
			dependencyGraph.Add(new DependencyComponent("library", "1.0.0", "library", "pkg:npm/library@1.0.0", "npm", Development: false, (IReadOnlyCollection<string>)(object)new string[1] { "MIT" }, tempFileName));
			using JsonDocument jsonDocument = JsonDocument.Parse(new CycloneDxWriter(new FixedClock(), new FixedIdentifierFactory()).Write(new CycloneDxReport("project", dependencyGraph)));
			Assert.Equal<string>("CycloneDX", jsonDocument.RootElement.GetProperty("bomFormat").GetString(), "CycloneDX format");
			Assert.Equal<string>("1.7", jsonDocument.RootElement.GetProperty("specVersion").GetString(), "CycloneDX specification version");
			Assert.Equal(2, jsonDocument.RootElement.GetProperty("components").GetArrayLength(), "CycloneDX component count");
			Assert.Equal<string>("application", jsonDocument.RootElement.GetProperty("metadata").GetProperty("component").GetProperty("type")
				.GetString(), "CycloneDX root component");
			JsonElement package = jsonDocument.RootElement.GetProperty("components").EnumerateArray().Single(component => component.GetProperty("purl").GetString() == "pkg:npm/library@1.0.0");
			Assert.Equal(0, package.GetProperty("hashes").GetArrayLength(), "CycloneDX package hashes");
			Assert.Equal<string>("pkg:npm/library@1.0.0", package.GetProperty("purl").GetString(), "CycloneDX package URL");
			Assert.Equal<string>("pkg:npm/library@1.0.0", package.GetProperty("bomRef").GetString(), "CycloneDX package BOM reference");
			Assert.Equal<string>("2026-11-04T00:00:00+00:00", jsonDocument.RootElement.GetProperty("metadata").GetProperty("timestamp").GetString(), "CycloneDX timestamp format");
		}
		finally
		{
			File.Delete(tempFileName);
		}
	}

	private static void CliOptionParsing()
	{
		ScanOptions scanOptions = ScanOptions.Parse(new string[7] { "./project", "--format", "cyclonedx", "--offline", "--output", "report.json", "--include-dev-deps" });
		Assert.Equal("./project", scanOptions.InputPath, "CLI input path");
		Assert.Equal<string>("report.json", scanOptions.OutputPath, "CLI output path");
		Assert.True(scanOptions.Offline, "CLI offline flag");
		Assert.True(scanOptions.IncludeDevDependencies, "CLI development dependency flag");
	}

	private static void CliValidation()
	{
		Assert.Throws<ScanException>(delegate
		{
			ScanOptions.Parse(new string[2] { "./project", "--format" });
		}, "missing format value");
		Assert.Throws<ScanException>(delegate
		{
			ScanOptions.Parse(new string[2] { "./project", "--output" });
		}, "missing output value");
		Assert.Throws<ScanException>(delegate
		{
			ScanOptions.Parse(new string[2] { "./project", "--unsupported" });
		}, "unknown option");
		Assert.Throws<HelpRequestedException>(delegate
		{
			ScanOptions.Parse(new string[1] { "--help" });
		}, "help request");
	}

	private static void DuplicatePackageInstances()
	{
		DependencyGraph dependencyGraph = new DependencyGraph();
		dependencyGraph.Add(new DependencyComponent("shared", "1.0", "library", "pkg:test/shared@1.0", "test", Development: false, (IReadOnlyCollection<string>)(object)Array.Empty<string>(), "/one.lock", null, 4, 4));
		dependencyGraph.Add(new DependencyComponent("shared", "1.0", "library", "pkg:test/shared@1.0", "test", Development: false, (IReadOnlyCollection<string>)(object)Array.Empty<string>(), "/two.lock", null, 9, 9));
		Assert.Equal(2, dependencyGraph.Components.Count, "duplicate package instances retained");
		Assert.True(dependencyGraph.Components.Select((DependencyComponent component) => component.InstanceId).Distinct().Count() == 2, "duplicate package instance IDs");
		Assert.True(dependencyGraph.Components.Any((DependencyComponent component) => component.StartLine == 4) && dependencyGraph.Components.Any((DependencyComponent component) => component.StartLine == 9), "source line ranges retained");
		Assert.Equal(2, dependencyGraph.PurlIndex["pkg:test/shared@1.0"].Count, "duplicate PURL index");
	}

	private static void SbomIdentityAndMetadata()
	{
		DependencyGraph dependencyGraph = new DependencyGraph();
		string[] licenses = new string[1] { "MIT" };
		IReadOnlyCollection<string> files = (IReadOnlyCollection<string>)(object)new string[1] { "library.jar" };
		IReadOnlyDictionary<string, string> qualifiers = new Dictionary<string, string> { ["classifier"] = "sources" };
		dependencyGraph.Add(new DependencyComponent("library", "1.0", "library", "pkg:maven/example/library@1.0", "maven", Development: false, (IReadOnlyCollection<string>)(object)licenses, "/pom.xml", null, null, null, null, "direct", "example", "Example Inc.", files, qualifiers, null, null, null, IsRoot: false, IsWorkspace: true));
		SbomDocument document = new SbomEncoder().Encode(new CycloneDxReport("project", dependencyGraph));
		SbomComponent packageComponent = document.Components.Single((SbomComponent component) => component.Purl == "pkg:maven/example/library@1.0");
		Assert.True(document.RootReference.StartsWith("urn:uuid:", StringComparison.Ordinal), "UUID root reference");
		Assert.True(packageComponent.IsWorkspace, "workspace role retained");
		Assert.Equal<string>("example", packageComponent.Group, "component group retained");
		Assert.Equal("sources", packageComponent.Qualifiers["classifier"], "component qualifiers retained");
		string instanceId = dependencyGraph.Components.Single().InstanceId;
		dependencyGraph.AddInstanceEdge(instanceId, instanceId);
		SbomRelationship sbomRelationship = new SbomEncoder().Encode(new CycloneDxReport("project", dependencyGraph)).Relationships.Single((SbomRelationship value) => value.Source == packageComponent.BomReference);
		Assert.Equal(packageComponent.BomReference, sbomRelationship.Targets.Single(), "relationship uses BOM reference");
	}

	private static void OfflineScannerDiscovery()
	{
		using TestProject testProject = TestProject.Create(("composer.lock", "{\"packages\":[]}"), ("gradle.lockfile", "org.example:runtime:1.0=compileClasspath\n"));
		TestDiagnostics testDiagnostics = new TestDiagnostics();
		OfflineScanner offlineScanner = new OfflineScanner(new IProjectAnalyzer[2]
		{
			new ComposerAnalyzer(testProject.FileSystem),
			new GradleAnalyzer(testProject.FileSystem)
		}, testProject.FileSystem, testDiagnostics);
		Assert.Equal(1, offlineScanner.Scan(new ScanOptions(testProject.Root, null, Offline: true, IncludeDevDependencies: true, null)).Graph.Components.Count, "scanner discovery");
		Assert.Equal(3, testDiagnostics.InfoMessages.Count, "scanner diagnostics");
	}

	private static void OfflineScannerFileTarget()
	{
		using TestProject testProject = TestProject.Create(("composer.lock", "{\"packages\":[]}"));
		OfflineScanner offlineScanner = new OfflineScanner(new IProjectAnalyzer[1]
		{
			new ComposerAnalyzer(testProject.FileSystem)
		}, testProject.FileSystem, new TestDiagnostics());
		CycloneDxReport cycloneDxReport = offlineScanner.Scan(new ScanOptions(Path.Combine(testProject.Root, "composer.lock"), null, Offline: true, IncludeDevDependencies: true, null));
		Assert.Equal<string>(testProject.Root, Path.GetDirectoryName(cycloneDxReport.InputPath), "file target path");
	}

	private static void MultipleLockfilesDuplicatePackage()
	{
		using TestProject testProject = TestProject.Create((Path.Combine("one", "composer.lock"), "{\"packages\":[{\"name\":\"vendor/shared\",\"version\":\"1.0.0\"}]}"), (Path.Combine("two", "composer.lock"), "{\"packages\":[{\"name\":\"vendor/shared\",\"version\":\"1.0.0\"}]}"));
		CycloneDxReport cycloneDxReport = new OfflineScanner(new IProjectAnalyzer[1]
		{
			new ComposerAnalyzer(testProject.FileSystem)
		}, testProject.FileSystem, new TestDiagnostics()).Scan(new ScanOptions(testProject.Root, null, Offline: true, IncludeDevDependencies: true, null));
		Assert.Equal(2, cycloneDxReport.Graph.Components.Count, "duplicate package across lockfiles");
		Assert.Equal(2, cycloneDxReport.Graph.PurlIndex["pkg:composer/vendor/shared@1.0.0"].Count, "duplicate package PURL index across lockfiles");
	}

	private static void MavenExclusionPathMediation()
	{
		using TestProject testProject = TestProject.Create(("pom.xml", "<project><dependencies><dependency><groupId>org.example</groupId><artifactId>left</artifactId><version>1.0</version></dependency><dependency><groupId>org.example</groupId><artifactId>right</artifactId><version>1.0</version></dependency></dependencies></project>"));
		CreateMavenPom(testProject.Root, "org.example", "left", "1.0", "<dependencies><dependency><groupId>org.example</groupId><artifactId>shared</artifactId><version>1.0</version><exclusions><exclusion><groupId>org.example</groupId><artifactId>blocked</artifactId></exclusion></exclusions></dependency></dependencies>");
		CreateMavenPom(testProject.Root, "org.example", "right", "1.0", "<dependencies><dependency><groupId>org.example</groupId><artifactId>shared</artifactId><version>1.0</version></dependency></dependencies>");
		CreateMavenPom(testProject.Root, "org.example", "shared", "1.0", "<dependencies><dependency><groupId>org.example</groupId><artifactId>blocked</artifactId><version>1.0</version></dependency></dependencies>");
		DependencyGraph dependencyGraph = Analyze(new MavenAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true, Path.Combine(testProject.Root, "m2"));
		Assert.True(dependencyGraph.Components.Any((DependencyComponent component) => component.Name == "org.example:blocked"), "unexcluded Maven path retains dependency");
		Assert.True(dependencyGraph.Edges["pkg:maven/org.example/left@1.0"].Contains("pkg:maven/org.example/shared@1.0"), "left path shared edge");
		Assert.True(dependencyGraph.Edges["pkg:maven/org.example/right@1.0"].Contains("pkg:maven/org.example/shared@1.0"), "right path shared edge");
	}

	private static void MavenModuleAndLicenseMetadata()
	{
		using TestProject testProject = TestProject.Create(("pom.xml", "<project><groupId>org.example</groupId><artifactId>root</artifactId><version>1.0</version><licenses><license><name>Apache-2.0</name></license></licenses><modules><module>module</module></modules></project>"), (Path.Combine("module", "pom.xml"), "<project><parent><groupId>org.example</groupId><artifactId>root</artifactId><version>1.0</version></parent><artifactId>module</artifactId><licenses><license><name>MIT</name></license></licenses></project>"));
		DependencyGraph dependencyGraph = Analyze(new MavenAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:root" && component.Licenses.Contains("Apache-2.0"), "Maven root license metadata");
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:module" && component.Licenses.Contains("MIT"), "Maven module license metadata");
		Assert.True(dependencyGraph.Edges["pkg:maven/org.example/root@1.0"].Contains("pkg:maven/org.example/module@1.0"), "Maven module relationship");
	}

	private static void MavenSettingsActiveProfile()
	{
		using TestProject testProject = TestProject.Create(("pom.xml", "<project><profiles><profile><id>ci</id><activation><activeByDefault>false</activeByDefault></activation><dependencies><dependency><groupId>org.example</groupId><artifactId>profile-lib</artifactId><version>1.0</version></dependency></dependencies></profile><profile><id>default-only</id><activation><activeByDefault>true</activeByDefault></activation><dependencies><dependency><groupId>org.example</groupId><artifactId>default-lib</artifactId><version>1.0</version></dependency></dependencies></profile></profiles></project>"));
		string text = Path.Combine(testProject.Root, ".m2");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "settings.xml"), "<settings><activeProfiles><activeProfile>ci</activeProfile></activeProfiles></settings>");
		DependencyGraph dependencyGraph = Analyze(new MavenAnalyzer(testProject.FileSystem, testProject.Root), testProject, includeDevDependencies: true);
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "org.example:profile-lib", "explicit Maven settings profile");
		Assert.True(!dependencyGraph.Components.Any((DependencyComponent component) => component.Name == "org.example:default-lib"), "active-by-default Maven profile suppressed");
	}

	private static void DartCompoundSdkConstraints()
	{
		using TestProject testProject = TestProject.Create(("pubspec.lock", "packages:\n  sdk_package:\n    dependency: transitive\n    source: sdk\n    version: \"0.0.0\"\n    description: dart\nsdks:\n  dart: \">=3.2.0 <4.0.0 || >=2.19.0 <3.0.0\"\n"));
		DependencyGraph dependencyGraph = Analyze(new DartAnalyzer(testProject.FileSystem), testProject, includeDevDependencies: true);
		Assert.Contains(dependencyGraph.Components, (DependencyComponent component) => component.Name == "sdk_package" && component.Version == "3.2.0", "Dart compound lower bound");
		using TestProject testProject2 = TestProject.Create(("pubspec.lock", "packages:\n  sdk_package:\n    dependency: transitive\n    source: sdk\n    version: \"0.0.0\"\n    description: dart\nsdks:\n  dart: \">3.2.0 <4.0.0\"\n"));
		DependencyGraph dependencyGraph2 = Analyze(new DartAnalyzer(testProject2.FileSystem), testProject2, includeDevDependencies: true);
		Assert.Contains(dependencyGraph2.Components, (DependencyComponent component) => component.Version == "0.0.0", "Dart unsupported strict constraint fallback");
	}

	private static DependencyGraph Analyze(IProjectAnalyzer analyzer, TestProject project, bool includeDevDependencies = false, string? mavenRepository = null)
	{
		DependencyGraph dependencyGraph = new DependencyGraph();
		analyzer.Analyze(project.Root, new ScanOptions(project.Root, null, Offline: true, includeDevDependencies, mavenRepository), dependencyGraph, new TestDiagnostics());
		return dependencyGraph;
	}
}
