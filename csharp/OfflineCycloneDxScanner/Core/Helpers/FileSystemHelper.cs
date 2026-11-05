using System;
using System.Collections.Generic;
using System.IO;

namespace OfflineCycloneDxScanner;

public static class FileSystemHelper
{
	public static IEnumerable<string> FindFiles(string root, string fileName)
	{
		if (File.Exists(root))
		{
			return (!string.Equals(Path.GetFileName(root), fileName, StringComparison.OrdinalIgnoreCase)) ? ((IEnumerable<string>)Array.Empty<string>()) : ((IEnumerable<string>)new string[1] { root });
		}
		return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories);
	}
}
