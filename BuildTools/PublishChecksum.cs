#!/usr/bin/env -S dotnet --
#:package AssetsTools.NET@3.0.5

#:include Common.cs

using System.Security.Cryptography;

if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
{
	Console.WriteLine("This script should be ran on Windows or Linux");
	return;
}

if (!Common.Initialize())
{
	return;
}

var checksumPath = Path.Combine(Common.PublishPath, $"BPLE-{Common.BuildVersion}-checksum.txt");
var verifierSourcePath = Path.Combine(Directory.GetCurrentDirectory(), "BuildTools", "VerifyChecksum.cs");
var verifierDestPath = Path.Combine(Common.PublishPath, $"BPLE-{Common.BuildVersion}-VerifyChecksum.cs");

using var writer = new StreamWriter(checksumPath);

foreach (var file in Directory.GetFiles(Common.PublishPath, "BPLE-*")
	         .Where(path => !path.Contains(".txt") && !path.Contains(".cs")))
{
	using var stream = File.OpenRead(file);
	var hash = SHA256.HashData(stream);
	var hex = Convert.ToHexStringLower(hash);
	writer.WriteLine($"{hex}  {Path.GetFileName(file)}");
}

File.Copy(verifierSourcePath, verifierDestPath, true);
