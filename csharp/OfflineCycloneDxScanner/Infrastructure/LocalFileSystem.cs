using System.Collections.Generic;
using System.IO;

namespace OfflineCycloneDxScanner;

public sealed class LocalFileSystem : IFileSystem
{
	public bool DirectoryExists(string path)
	{
		return Directory.Exists(path);
	}

	public bool FileExists(string path)
	{
		return File.Exists(path);
	}

	public IEnumerable<string> FindFiles(string root, string fileName)
	{
		return FileSystemHelper.FindFiles(root, fileName);
	}

	public string ReadAllText(string path)
	{
		return File.ReadAllText(path);
	}

	public IEnumerable<string> ReadLines(string path)
	{
		return File.ReadLines(path);
	}
}
