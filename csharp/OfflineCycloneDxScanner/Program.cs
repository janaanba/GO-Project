using System;
using System.IO;
using System.Runtime.CompilerServices;
using OfflineCycloneDxScanner;

[CompilerGenerated]
internal class Program
{
	private static int Main(string[] args)
	{
		ConsoleDiagnostics consoleDiagnostics = new ConsoleDiagnostics();
		try
		{
			ScanOptions scanOptions = ScanOptions.Parse(args);
			LocalFileSystem fileSystem = new LocalFileSystem();
			OfflineScanner offlineScanner = new OfflineScanner(new IProjectAnalyzer[5]
			{
				new ConanAnalyzer(fileSystem),
				new DartAnalyzer(fileSystem),
				new ComposerAnalyzer(fileSystem),
				new MavenAnalyzer(fileSystem),
				new GradleAnalyzer(fileSystem)
			}, fileSystem, consoleDiagnostics);
			CycloneDxReport report = offlineScanner.Scan(scanOptions);
			string text = new CycloneDxWriter(new SystemClock(), new GuidIdentifierFactory()).Write(report);
			if (scanOptions.OutputPath == null)
			{
				Console.Out.WriteLine(text);
			}
			else
			{
				File.WriteAllText(scanOptions.OutputPath, text);
			}
		}
		catch (ScanException ex)
		{
			consoleDiagnostics.Error(ex.Message);
			return 2;
		}
		catch (HelpRequestedException)
		{
			Console.Error.WriteLine("Usage: dotnet run -- <input> [--format cyclonedx] [--output file] [--offline-scan] [--include-dev-deps] [--maven-local-repository path]");
			return 0;
		}
		catch (Exception ex3)
		{
			consoleDiagnostics.Error("Scan failed: " + ex3.Message);
			return 1;
		}
		return 0;
	}
}
