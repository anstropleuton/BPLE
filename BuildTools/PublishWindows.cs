#!/usr/bin/env -S dotnet --
#:package Magick.NET-Q8-AnyCPU@14.15.0
#:package AssetsTools.NET@3.0.5

#:include Common.cs

using System.Diagnostics;
using System.IO.Compression;
using ImageMagick;

if (!OperatingSystem.IsWindows())
{
    Console.WriteLine("This script must be ran on Windows");
    Environment.Exit(1);
}

if (!Common.Initialize())
{
	Environment.Exit(1);
}

var iconOrgPath = Path.Combine(Common.AssetsPath, "Texture2D", "App Icon.png");

// 256x icon .ico
var iconIcoPath = Path.Combine(Common.TempPath, "icon.ico");

using (var image = new MagickImage(File.ReadAllBytes(iconOrgPath)))
{
    image.Resize(256, 256);
    image.Write(iconIcoPath, MagickFormat.Ico);
}

// Download tools

// Inno Setup itself has only installer released
// Extract it using Inno Unpacker
var ipArchivePath = Path.Combine(Common.TempPath, "ip.zip");
if (!File.Exists(ipArchivePath))
{
    var ipUrl = "https://rathlev-home.de/tools/download/innounpacker.zip";
    if (!Common.DownloadFile(ipUrl, ipArchivePath))
    {
	    Environment.Exit(1);
    }
}

var ipExtractPath = Path.Combine(Common.TempPath, "ip");
var ipBinPath = Path.Combine(ipExtractPath, "innounp.exe");
if (!Directory.Exists(ipExtractPath) || !File.Exists(ipBinPath))
{
    Directory.CreateDirectory(ipExtractPath);

    ZipFile.ExtractToDirectory(ipArchivePath, ipExtractPath, true);
}

// Inno Setup
var isArchivePath = Path.Combine(Common.TempPath, "is.exe"); // Treating exe as archive
if (!File.Exists(isArchivePath))
{
    var isUrl = "https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe";
    if (!Common.DownloadFile(isUrl, isArchivePath))
    {
	    Environment.Exit(1);
    }
}

var isExtractPath = Path.Combine(Common.TempPath, "is");
var isBinPath = Path.Combine(isExtractPath, "{app}", "ISCC.exe");
if (!Directory.Exists(isExtractPath) || !File.Exists(isBinPath))
{
	var exitCode = Common.RunCommand(ipBinPath, ["-x", "-b", $"-d{isExtractPath}", "-a", "-y", isArchivePath]);
	if (exitCode != 0)
	{
		Environment.Exit(exitCode);
	}
}

foreach (var (current, arch) in ((string, string)[])[("WindowsX32", "x32"), ("WindowsX64", "x64")])
{
    var buildPath = Path.Combine(Common.BuildsPath, current);

    if (!Directory.Exists(buildPath))
    {
        Console.WriteLine($"Warning: Build for {current} does not exist: {buildPath}; Skipping it");
        continue;
    }

    Console.WriteLine($"Preparing {current}");

    // Exclude backup
    var stagingPath = Path.Combine(Common.TempPath, ".stage", current);
    Common.StageBuild(buildPath, stagingPath);

    // Hacks
    var ggmPath = Path.Combine(stagingPath, $"BPLE-{Common.BuildVersion}_Data", "globalgamemanagers");
    Common.PatchGgm(ggmPath);

    // Zip
    var buildZip = Path.Combine(Common.PublishPath, $"BPLE-{Common.BuildVersion}-windows-{arch}.zip");
    if (File.Exists(buildZip)) File.Delete(buildZip);
    Console.WriteLine($"Building {buildZip}");
    ZipFile.CreateFromDirectory(stagingPath, buildZip);

    // Installer
    var buildExe = Path.Combine(Common.PublishPath, $"BPLE-{Common.BuildVersion}-windows-{arch}.exe");
    Console.WriteLine($"Building {buildExe}");

    var allowedArch = arch switch
    {
        "x32" => "x86compatible",
        "x64" => "x64compatible",
        _ => throw new UnreachableException()
    };

    var imageFile = Path.Combine(Common.ToolsPath, "Background.png");
    var smallImageFile = Path.Combine(Common.ToolsPath, "Icon.png");

    var scriptPath = Path.Combine(Common.TempPath, "installer.iss");
    File.WriteAllText(scriptPath,
        $$"""
		  [Setup]
		  AppId=BPLE-{{Common.BuildVersion}}
		  AppName=BPLE {{Common.BuildVersion}}
		  AppVersion={{Common.BuildVersion}}
		  AppVerName=BPLE {{Common.BuildVersion}}
		  AppPublisher=Anstro Pleuton
		  AppComments=BPLE is a modification of the game Bad Piggies.

		  PrivilegesRequired=lowest
		  PrivilegesRequiredOverridesAllowed=dialog commandline

		  DefaultDirName={autopf}\BPLE {{Common.BuildVersion}}
		  DefaultGroupName=BPLE
		  AllowNoIcons=yes
		  AllowRootDirectory=yes

		  SolidCompression=yes
		  OutputDir={{Common.PublishPath}}
		  OutputBaseFilename=BPLE-{{Common.BuildVersion}}-windows-{{arch}}

		  SetupIconFile={{iconIcoPath}}
		  ArchitecturesAllowed={{allowedArch}}

		  WizardStyle=modern dynamic windows11
		  WizardImageFile={{imageFile}}
		  WizardSmallImageFile={{smallImageFile}}

		  UninstallDisplayIcon={app}\BPLE-{{Common.BuildVersion}}.exe

		  [Files]
		  Source: "{{stagingPath}}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

		  [Icons]
		  Name: "{group}\BPLE {{Common.BuildVersion}}"; Filename: "{app}\BPLE-{{Common.BuildVersion}}.exe"
		  Name: "{group}\Uninstall BPLE {{Common.BuildVersion}}"; Filename: "{uninstallexe}"
		  Name: "{autodesktop}\BPLE {{Common.BuildVersion}}"; Filename: "{app}\BPLE-{{Common.BuildVersion}}.exe"; Tasks: desktopicon

		  [Tasks]
		  Name: desktopicon; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"
		  """);

    Environment.ExitCode += Common.RunCommand(isBinPath, [scriptPath]);
}