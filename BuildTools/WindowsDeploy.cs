#!/usr/bin/env -S dotnet --
#:package WixSharp_wix4@2.14.1
#:package System.Drawing.Common@10.0.2

using System.Diagnostics;
using System.IO.Compression;
using WixSharp;

if (Path.GetFileName(Directory.GetCurrentDirectory()) == "BuildTools")
{
    Directory.SetCurrentDirectory(Path.GetDirectoryName(Directory.GetCurrentDirectory())!);
}

if (Path.GetFileName(Directory.GetCurrentDirectory()) != "BPLE")
{
    Console.WriteLine("The script must be ran in BPLE directory");
}

var psPath = Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "ProjectSettings.asset");
string? readVersion = null;

using (var reader = new StreamReader(psPath))
{
    while (!reader.EndOfStream)
    {
        var line = reader.ReadLine()!;
        if (line.Contains("bundleVersion"))
        {
            readVersion = line[(line.FindIndex(':') + 1)..].Trim();
            break;
        }
    }
}

if (readVersion is null)
{
    Console.WriteLine("Version information not found in ProjectSettings/ProjectSettings.asset");
}

var buildVersion = readVersion!;

var publishPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Publish");

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

    if (!Directory.Exists(publishPath))
        Directory.CreateDirectory(publishPath);

    // Zip
    var buildZip = Path.Combine(publishPath, $"BPLE-{buildVersion}-windows-{buildArchitecture}.zip");
    if (System.IO.File.Exists(buildZip)) System.IO.File.Delete(buildZip);
    ZipFile.CreateFromDirectory(buildPath, buildZip);

    // Installer
    var buildMsi = Path.Combine(publishPath, $"BPLE-{buildVersion}-windows-{buildArchitecture}-setup.msi");
    if (System.IO.File.Exists(buildMsi)) System.IO.File.Delete(buildMsi);

    var project = new Project("BPLE",
        new Dir(
            Path.Combine("%ProgramFiles%", $"BPLE {buildVersion}"),
            new DirFiles(Path.Combine(buildPath, "*.*"))
        )
    )
    {
        // Strip 20 from 2022.1* because apparently MSI major version must be less than 256 like wth
        Version = buildVersion.Contains('-')
            ? new Version(buildVersion[..buildVersion.FindIndex('-')][2..])
            : new Version(buildVersion[2..]),

        GUID = new Guid("913a6936-68cc-4365-8b96-be7c1cf17126"),

        OutDir = Path.GetDirectoryName(buildMsi),
        OutFileName = Path.GetFileNameWithoutExtension(buildMsi)
    };

    Compiler.BuildMsi(project);
}