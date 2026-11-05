using System;
using System.Collections.Generic;
using System.Linq;

namespace OfflineCycloneDxScanner;

public sealed record ScanOptions(string InputPath, string? OutputPath, bool Offline, bool IncludeDevDependencies, string? MavenRepository)
{
	public static ScanOptions Parse(string[] args)
	{
		if (args.Contains("--help") || args.Contains("-h"))
		{
			throw new HelpRequestedException();
		}
		List<string> list = new List<string>();
		string[] source = new string[3] { "--output", "--format", "--maven-local-repository" };
		string[] source2 = new string[3] { "--offline-scan", "--offline", "--include-dev-deps" };
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i].StartsWith("-", StringComparison.Ordinal) && !source.Contains<string>(args[i], StringComparer.Ordinal) && !source2.Contains<string>(args[i], StringComparer.Ordinal))
			{
				throw new ScanException("Unknown option " + args[i] + ".");
			}
			if (source.Contains<string>(args[i], StringComparer.Ordinal))
			{
				if (i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
				{
					throw new ScanException("Missing value for " + args[i] + ".");
				}
				i++;
			}
			else if (!args[i].StartsWith("--", StringComparison.Ordinal))
			{
				list.Add(args[i]);
			}
		}
		if (list.Count != 1)
		{
			throw new ScanException("Exactly one input file or directory is required.");
		}
		string text = Value(args, "--format");
		if (text != null && !string.Equals(text, "cyclonedx", StringComparison.OrdinalIgnoreCase))
		{
			throw new ScanException("Only --format cyclonedx is supported.");
		}
		return new ScanOptions(list[0], Value(args, "--output"), args.Contains("--offline-scan") || args.Contains("--offline"), args.Contains("--include-dev-deps"), Value(args, "--maven-local-repository"));
	}

	private static string? Value(string[] args, string name)
	{
		int num = Array.IndexOf(args, name);
		return (num >= 0 && num + 1 < args.Length) ? args[num + 1] : null;
	}
}
