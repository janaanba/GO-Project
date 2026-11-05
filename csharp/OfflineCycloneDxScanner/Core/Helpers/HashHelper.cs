using System;
using System.IO;
using System.Security.Cryptography;

namespace OfflineCycloneDxScanner;

public static class HashHelper
{
	public static string Sha256(string path)
	{
		using FileStream source = File.OpenRead(path);
		return Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
	}
}
