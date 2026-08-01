#!/usr/bin/env -S dotnet --

// Useless looking script

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

var psPath = Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "ProjectSettings.asset");

var buildVersion = File.ReadLines(psPath)
	.FirstOrDefault(line => line.Contains("bundleVersion"))!
	.Split(':', 2)[1]
	.Trim();

var publishPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Publish");

foreach (var currentBuild in (string[])["Android"])
{
	var buildPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", currentBuild);

	if (!Directory.Exists(buildPath))
	{
		Console.WriteLine($"Warning: Build for {currentBuild} does not exist: {buildPath}; Skipping it");
		continue;
	}

	if (!Directory.Exists(publishPath)) Directory.CreateDirectory(publishPath);

	var buildApk = Path.Combine(publishPath, $"BPLE-{buildVersion}-android.apk");
	File.Copy(Path.Combine(buildPath, $"BPLE-{buildVersion}.apk"), buildApk);
}
