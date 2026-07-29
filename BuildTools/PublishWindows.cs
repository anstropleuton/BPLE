#!/usr/bin/env -S dotnet --
#:package WixSharp_wix4@2.14.1
#:package System.Drawing.Common@10.0.2
#:package Magick.NET-Q8-AnyCPU@14.15.0

using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ImageMagick;
using WixSharp;
using File = System.IO.File;

if (!OperatingSystem.IsWindows())
{
    Console.WriteLine("This script should be ran on Windows");
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

var iconOrgPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Texture2D", "App Icon.png");

var tempPath = Path.Combine(publishPath, ".temp");
if (!Directory.Exists(tempPath)) Directory.CreateDirectory(tempPath);

// 256x icon .ico
var iconIcoPath = Path.Combine(tempPath, "icon.ico");

using (var image = new MagickImage(File.ReadAllBytes(iconOrgPath)))
{
    image.Resize(256, 256);
    image.Write(iconIcoPath, MagickFormat.Ico);
}

foreach (var currentBuild in (string[])["StandaloneWindows", "StandaloneWindows64"])
{
    var buildPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", currentBuild);

    if (!Directory.Exists(buildPath))
    {
        Console.WriteLine($"Warning: Build for {currentBuild} does not exist: {buildPath}; Skipping it");
        continue;
    }

    var buildArchitecture = currentBuild switch
    {
        "StandaloneWindows" => "x32",
        "StandaloneWindows64" => "x64",
        _ => throw new UnreachableException()
    };

    if (!Directory.Exists(publishPath)) Directory.CreateDirectory(publishPath);

    // Exclude backup
    var stagingPath = Path.Combine(publishPath, ".stage", currentBuild);
    if (Directory.Exists(stagingPath)) Directory.Delete(stagingPath, true);
    Directory.CreateDirectory(stagingPath);

    CopyForStaging(buildPath, stagingPath);

    // Zip
    var buildZip = Path.Combine(publishPath, $"BPLE-{buildVersion}-windows-{buildArchitecture}.zip");
    if (File.Exists(buildZip)) File.Delete(buildZip);
    Console.WriteLine($"Building {buildZip}");
    ZipFile.CreateFromDirectory(stagingPath, buildZip);

    // Installer
    var buildMsi = Path.Combine(publishPath, $"BPLE-{buildVersion}-windows-{buildArchitecture}-setup.msi");
    if (File.Exists(buildMsi)) File.Delete(buildMsi);
    Console.WriteLine($"Building {buildMsi}");

    var buildHash = MD5.HashData(Encoding.UTF8.GetBytes($"BPLE-{buildVersion}-{currentBuild}"));
    
    var programFilesDir = buildArchitecture switch
    {
        "x64" => "%ProgramFiles64%",
        "x32" => "%ProgramFiles%",
        _ => throw new UnreachableException()
    };

    var wixProject = new Project("BPLE",
        new Dir(
            Path.Combine(programFilesDir, $"BPLE {buildVersion}"),
            new DirFiles(Path.Combine(stagingPath, "*.*")),
            new Dir("%ProgramMenu%", new ExeFileShortcut("BPLE", $"[INSTALLDIR]BPLE-{buildVersion}.exe", "")
                {
                    Description = "Launch BPLE"
                }
            )
        )
    )
    {
        // Strip 20 from 2022.1* because apparently MSI major version must be less than 256 like wth
        Version = new Version(buildVersion[2..].Split('-', 2)[0]),
        Platform = buildArchitecture switch
        {
            "x64" => Platform.x64,
            "x32" => Platform.x86,
            _ => throw new UnreachableException()
        },
        Description = "BPLE is a modification of the game Bad Piggies.",
        GUID = new Guid(buildHash),
        OutDir = Path.GetDirectoryName(buildMsi),
        OutFileName = Path.GetFileNameWithoutExtension(buildMsi),
        ControlPanelInfo = new ProductInfo
        {
            Manufacturer = "Anstro Pleuton",
            ProductIcon = iconIcoPath
        }
    };

    Compiler.BuildMsi(wixProject);
    continue;

    void CopyForStaging(string source, string dest)
    {
        Directory.CreateDirectory(dest);

        foreach (var dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            if (dirPath.Contains("BackUpThisFolder_ButDontShipItWithYourGame")) continue;

            var relative = Path.GetRelativePath(source, dirPath);
            Directory.CreateDirectory(Path.Combine(dest, relative));
        }

        foreach (var filePath in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (filePath.Contains("BackUpThisFolder_ButDontShipItWithYourGame")) continue;

            var relative = Path.GetRelativePath(source, filePath);
            File.Copy(filePath, Path.Combine(dest, relative), true);
        }
    }
}
