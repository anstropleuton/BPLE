#!/usr/bin/env -S dotnet --
#:package AssetsTools.NET@3.0.5

#:include Common.cs

using System.Text.RegularExpressions;

if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
{
	Console.WriteLine("This script must be ran on Windows or Linux");
	Environment.Exit(1);
}

if (!Common.Initialize())
{
	Environment.Exit(1);
}

// Check Java
string[] javaFilenames = ["java.exe", "java"];
var javaPath = "";

var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME")!;
if (!string.IsNullOrEmpty(javaHome))
{
	foreach (var javaFilename in javaFilenames)
	{
		var fullPath = Path.Combine(javaHome, "bin", javaFilename);
		if (File.Exists(fullPath)) javaPath = fullPath;
	}
}

if (string.IsNullOrEmpty(javaPath))
{
	var pathVar = Environment.GetEnvironmentVariable("PATH");
	if (!string.IsNullOrEmpty(pathVar))
	{
		foreach (var directory in pathVar.Split(Path.PathSeparator))
		{
			foreach (var javaFilename in javaFilenames)
			{
				var fullPath = Path.Combine(directory.Trim(), javaFilename);
				if (File.Exists(fullPath))
				{
					javaPath = fullPath;
				}
			}
		}
	}
}

if (string.IsNullOrEmpty(javaPath))
{
	Console.WriteLine("Java (8+) is required");
	Environment.Exit(1);
}

var ktPath = javaFilenames.Aggregate(javaPath,
	(current, javaFilename) => Regex.Replace(current, $"{javaFilename}$", javaFilename.Replace("java", "keytool")));

// Download tools

// APK Tool
var atPath = Path.Combine(Common.TempPath, "at.jar");
if (!File.Exists(atPath))
{
	var atUrl = "https://bitbucket.org/iBotPeaches/apktool/downloads/apktool_3.0.3.jar";
	Common.DownloadFile(atUrl, atPath);
}

// Uber APK Signer
var asPath = Path.Combine(Common.TempPath, "as.jar");
if (!File.Exists(asPath))
{
	var asUrl = "https://github.com/patrickfav/uber-apk-signer/releases/download/v1.3.0/uber-apk-signer-1.3.0.jar";
	Common.DownloadFile(asUrl, asPath);
}

// Prepare signing key
var keyPass = "bple-team";

var keyPath = Path.Combine(Common.ToolsDataPath, "bple-keystore.jks");
if (!File.Exists(keyPath))
{
	var exitCode = Common.RunCommand(ktPath, [
		"-genkey", "-keystore", keyPath, "-keyalg", "RSA",
		"-keysize", "2048", "-validity", "10000", "-alias", "bple-keystore",
		"-storepass", keyPass, "-keypass", keyPass,
		"-dname", "CN=BPLE Team, OU=BPLE Team, O=BPLE Team, L=BPLE City, ST=BPLE State, C=US"
	]);

	if (exitCode != 0)
	{
		Environment.Exit(exitCode);
	}
}

foreach (var (current, arch) in ((string, string)[])[("AndroidArm", "arm"), ("AndroidIntel", "intel")])
{
	var buildPath = Path.Combine(Common.BuildsPath, current);

	if (!Directory.Exists(buildPath))
	{
		Console.WriteLine($"Warning: Build for {current} does not exist: {buildPath}; Skipping it");
		continue;
	}

	Console.WriteLine($"Preparing {current}");

	var oApkPath = Path.Combine(buildPath, $"BPLE-{Common.BuildVersion}.apk");

	// Decode
	var decodePath = Path.Combine(Common.TempPath, ".decode", current);
	if (Directory.Exists(decodePath)) Directory.Delete(decodePath, true);
	Directory.CreateDirectory(decodePath);

	Console.WriteLine($"Decoding {oApkPath}");
	if ((Environment.ExitCode += Common.RunCommand(javaPath, [
		    "-jar",
		    atPath, "d", oApkPath, "-o", decodePath, "-f"
	    ])) != 0)
	{
		continue;
	}

	// Lower SDK
	var configFile = Path.Combine(decodePath, "apktool.yml");
	var configLines = File.ReadAllLines(configFile);
	File.WriteAllLines(configFile,
		configLines.Select(line => line.Contains("minSdkVersion") ? "  minSdkVersion: 19" : line));

	// Hacks
	var ggmPath = Path.Combine(decodePath, "assets", "bin", "Data", "globalgamemanagers");
	Common.PatchGgm(ggmPath);

	// Build
	var buildApk = Path.Combine(Common.PublishPath, $"BPLE-{Common.BuildVersion}-android-{arch}.apk");
	Console.WriteLine($"Building {buildApk}");

	if ((Environment.ExitCode += Common.RunCommand(javaPath, [
		    "-jar",
		    atPath, "b", decodePath, "-o", buildApk
	    ])) != 0)
	{
		continue;
	}

	if ((Environment.ExitCode += Common.RunCommand(javaPath, ["-jar",
		    asPath, "-a", buildApk, "--overwrite",
		    "--ks", keyPath, "--ksAlias", "bple-keystore", "--ksKeyPass", keyPass, "--ksPass", keyPass
	    ])) != 0)
	{
		continue;
	}

	var sigPath = buildApk + ".idsig";
	if (File.Exists(sigPath)) File.Delete(sigPath);
}