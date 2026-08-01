#!/usr/bin/env -S dotnet --

using System.Security.Cryptography;

var checksumFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*checksum.txt");

var missing = 0;
var ok = 0;
var failed = 0;

foreach (var checksumFile in checksumFiles)
{
	foreach (var line in File.ReadLines(checksumFile))
	{
		if (string.IsNullOrWhiteSpace(line)) continue;
		
		var parts = line.Split("  ", 2);
		if (parts.Length != 2)
		{
			Console.WriteLine($"Skipping malformed line: {line}");
			continue;
		}

		var expectedHashHex = parts[0].Trim();
		var fileName = parts[1].Trim();

		if (!File.Exists(fileName))
		{
			Console.WriteLine($"MISSING  {fileName}");
			missing++;
			continue;
		}

		using var stream = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), fileName));
		var actualHash = SHA256.HashData(stream);
		var actualHashHex = Convert.ToHexStringLower(actualHash);

		if (expectedHashHex == actualHashHex)
		{
			Console.WriteLine($"OK       {fileName}");
			ok++;
		}
		else
		{
			Console.WriteLine($"FAILED   {fileName}");
			failed++;
		}
	}
}

Console.WriteLine();
Console.WriteLine($"Summary:");
Console.WriteLine($"  {ok,4} OK");
Console.WriteLine($"  {failed,4} FAILED");
Console.WriteLine($"  {missing,4} MISSING");

if (failed > 0 || missing > 0) Environment.Exit(1);
