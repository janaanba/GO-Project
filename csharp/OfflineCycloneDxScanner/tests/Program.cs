using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OfflineCycloneDxScanner;

[CompilerGenerated]
internal class Program
{
	private static void Main(string[] args)
	{
		string text = Path.Combine(Path.GetTempPath(), $"offline-cyclonedx-{Guid.NewGuid():N}");
		Directory.CreateDirectory(text);
		try
		{
			File.WriteAllText(Path.Combine(text, "conan.lock"), "{\"graph_lock\":{\"nodes\":{\"0\":{\"ref\":\"\",\"requires\":[\"1\"]},\"1\":{\"ref\":\"zlib/1.2.13\",\"requires\":[]}}}}");
			File.WriteAllText(Path.Combine(text, "pubspec.lock"), "packages:\n  http:\n    dependency: \"direct main\"\n    version: \"1.2.0\"\n");
			File.WriteAllText(Path.Combine(text, "composer.lock"), "{\"packages\":[{\"name\":\"vendor/lib\",\"version\":\"1.0.0\",\"require\":{}}]}");
			File.WriteAllText(Path.Combine(text, "gradle.lockfile"), "org.example:library:1.0=compileClasspath\n");
						File.WriteAllText(Path.Combine(text, "pom.xml"), "<project><groupId>org.example</groupId><artifactId>sample-app</artifactId><version>1.0.0</version><dependencies><dependency><groupId>org.example</groupId><artifactId>maven-lib</artifactId><version>1.0.0</version></dependency></dependencies></project>");
			LocalFileSystem fileSystem = new LocalFileSystem();
			OfflineScanner offlineScanner = new OfflineScanner(new IProjectAnalyzer[5]
			{
				new ConanAnalyzer(fileSystem),
				new DartAnalyzer(fileSystem),
				new ComposerAnalyzer(fileSystem),
				new MavenAnalyzer(fileSystem),
				new GradleAnalyzer(fileSystem)
			}, fileSystem, new NullDiagnostics());
			CycloneDxReport cycloneDxReport = offlineScanner.Scan(new ScanOptions(text, null, Offline: true, IncludeDevDependencies: false, null));
						Assert(cycloneDxReport.Graph.Components.Count >= 4, "supported smoke ecosystems should be discovered");
			string json = new CycloneDxWriter(new SystemClock(), new GuidIdentifierFactory()).Write(cycloneDxReport);
			using JsonDocument jsonDocument = JsonDocument.Parse(json);
			Assert(jsonDocument.RootElement.GetProperty("bomFormat").GetString() == "CycloneDX", "CycloneDX format should be emitted");
						Assert(jsonDocument.RootElement.GetProperty("components").GetArrayLength() >= 4, "all discovered components should be emitted");
			Console.WriteLine("Smoke tests passed.");
			TestSuite.RunAll();
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
		static void Assert(bool condition, string message)
		{
			if (!condition)
			{
				throw new InvalidOperationException(message);
			}
		}
	}
}
