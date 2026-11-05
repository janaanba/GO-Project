using System.Collections.Generic;

namespace OfflineCycloneDxScanner;

public interface IFileSystem
{
	bool DirectoryExists(string path);

	bool FileExists(string path);

	IEnumerable<string> FindFiles(string root, string fileName);

	string ReadAllText(string path);

	IEnumerable<string> ReadLines(string path);
}
