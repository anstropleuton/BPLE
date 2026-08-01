#!/usr/bin/env -S dotnet --

using System.Security.Cryptography;

if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
{
	Console.WriteLine("This script should be ran on Windows or Linux");
	return;
}

if (Path.GetFileName(Directory.GetCurrentDirectory()) == "BuildTools")
{
	Directory.SetCurrentDirectory(Path.GetDirectoryName(Directory.GetCurrentDirectory())!);
}

if (Path.GetFileName(Directory.GetCurrentDirectory()) != "BPLE")
{
	Console.WriteLine("The script must be ran in BPLE directory");
	return;
}

var publishPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Publish");

var psPath = Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "ProjectSettings.asset");

var buildVersion = File.ReadLines(psPath)
	.FirstOrDefault(line => line.Contains("bundleVersion"))!
	.Split(':', 2)[1]
	.Trim();

var checksumPath = Path.Combine(publishPath, $"BPLE-{buildVersion}-checksum.txt");
var verifierSourcePath = Path.Combine(Directory.GetCurrentDirectory(), "BuildTools", "VerifyChecksum.cs");
var verifierDestPath = Path.Combine(publishPath, $"BPLE-{buildVersion}-VerifyChecksum.cs");

using var writer = new StreamWriter(checksumPath);

foreach (var file in Directory.GetFiles(publishPath, "BPLE-*")
	         .Where(path => !path.Contains(".txt") && !path.Contains(".cs")))
{
	using var stream = File.OpenRead(file);
	var hash = SHA256.HashData(stream);
	var hex = Convert.ToHexStringLower(hash);
	writer.WriteLine($"{hex}  {Path.GetFileName(file)}");
}

File.Copy(verifierSourcePath, verifierDestPath, true);
