#!/usr/bin/env -S dotnet --
#:package Magick.NET-Q8-AnyCPU@14.15.0

using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using ImageMagick;

if (!OperatingSystem.IsLinux())
{
    Console.WriteLine("This script should be ran on Linux");
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

// 256x icon
var icon256Path = Path.Combine(tempPath, "icon-256.png");

using (var image = new MagickImage(File.ReadAllBytes(iconOrgPath)))
{
    image.Resize(256, 256);
    image.Write(icon256Path, MagickFormat.Png);
}

// 512x icon
var icon512Path = Path.Combine(tempPath, "icon-512.png");

using (var image = new MagickImage(File.ReadAllBytes(iconOrgPath)))
{
    image.Resize(512, 512);
    image.Write(icon512Path, MagickFormat.Png);
}

// Download tools
using var client = new HttpClient();

// nFPM
var nfpmArchivePath = Path.Combine(tempPath, "nfpm.tar.gz");
if (!File.Exists(nfpmArchivePath))
{
    var nfpmUrl = "https://github.com/goreleaser/nfpm/releases/download/v2.47.0/nfpm_2.47.0_Linux_x86_64.tar.gz";
    DownloadFile(nfpmUrl, nfpmArchivePath);
}

var nfpmExtractPath = Path.Combine(tempPath, "nfpm");
var nfpmBinPath = Path.Combine(nfpmExtractPath, "nfpm");
if (!Directory.Exists(nfpmExtractPath) || !File.Exists(nfpmBinPath))
{
    Directory.CreateDirectory(nfpmExtractPath);

    using var archiveStream = File.OpenRead(nfpmArchivePath);
    using var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress);

    TarFile.ExtractToDirectory(gzipStream, nfpmExtractPath, true);
}

// AppImageTool
var aitPath = Path.Combine(tempPath, "ait.AppImage");
if (!File.Exists(aitPath))
{
    var aitUrl = "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage";
    DownloadFile(aitUrl, aitPath, true);
}

foreach (var currentBuild in (string[])["StandaloneLinux64"])
{
    var buildPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", currentBuild);

    if (!Directory.Exists(buildPath))
    {
        Console.WriteLine($"Warning: Build for {currentBuild} does not exist: {buildPath}; Skipping it");
        continue;
    }

    var buildArchitecture = currentBuild switch
    {
        "StandaloneLinux64" => "x64",
        _ => throw new UnreachableException()
    };

    if (!Directory.Exists(publishPath)) Directory.CreateDirectory(publishPath);

    // Exclude backup
    var stagingPath = Path.Combine(publishPath, ".stage", currentBuild);
    if (Directory.Exists(stagingPath)) Directory.Delete(stagingPath, true);
    Directory.CreateDirectory(stagingPath);

    CopyForStaging(buildPath, stagingPath);

    // Tar.gz
    var buildTar = Path.Combine(publishPath, $"BPLE-{buildVersion}-linux-{buildArchitecture}.tar.gz");
    if (File.Exists(buildTar)) File.Delete(buildTar);

    using (var fileStream = File.Create(buildTar))
    using (var gzipStream = new GZipStream(fileStream, CompressionMode.Compress))
    {
        TarFile.CreateFromDirectory(stagingPath, gzipStream, false);
    }

    // Packages
    var desktopPath = Path.Combine(tempPath, $"BPLE-{buildVersion}.desktop");
    File.WriteAllText(desktopPath,
        $"""
             [Desktop Entry]
             Name=BPLE {buildVersion}
             Comment=BPLE is a modification of the game Bad Piggies.
             Exec=/opt/bple-{buildVersion}/BPLE-{buildVersion}.x86_64
             Path=/opt/bple-{buildVersion}/
             Icon=BPLE-{buildVersion}
             Terminal=false
             Type=Application
             Categories=Game;
             StartupWMClass=BPLE-{buildVersion}
             """.Replace("\r\n", "\n"));

    var configPath = Path.Combine(tempPath, "nfpm.yaml");
    File.WriteAllText(configPath,
        $"""
         name: "bple-{buildVersion}"
         arch: "amd64"
         version: "{buildVersion.Replace('-', '.')}"
         maintainer: "Anstro Pleuton"
         description: "BPLE is a modification of the game Bad Piggies."
         contents:
           - src: "{stagingPath}/"
             dst: "/opt/bple-{buildVersion}"
             type: "tree"
           - src: "/opt/bple-{buildVersion}/BPLE-{buildVersion}.x86_64"
             dst: "/usr/bin/BPLE-{buildVersion}"
             type: "symlink"
           - src: "{desktopPath}"
             dst: "/usr/share/applications/BPLE-{buildVersion}.desktop"
           - src: "{icon512Path}"
             dst: "/usr/share/icons/hicolor/512x512/apps/BPLE-{buildVersion}.png"
         """);

    foreach (var (package, extension) in ((string, string)[])
             [
                 ("apk", "apk"), ("archlinux", "pkg.tar.zst"), ("deb", "deb"), ("ipk", "ipk"), ("rpm", "rpm")
             ])
    {
        var targetPath = Path.Combine(publishPath, $"BPLE-{buildVersion}-linux-{buildArchitecture}.{extension}");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = nfpmBinPath,
            ArgumentList = { "package", "-f", configPath, "-p", package, "-t", targetPath },
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit();
        if (process?.ExitCode != 0)
        {
            Console.WriteLine($"nFPM exited with non-zero exit code: {process?.ExitCode}");
        }
    }

    // AppImage
    var aitDirPath = Path.Combine(tempPath, $"bple-{buildVersion}.AppDir");
    if (Directory.Exists(aitDirPath)) Directory.Delete(aitDirPath, true);

    CopyFilesRecursively(stagingPath, Path.Combine(aitDirPath, "opt", $"bple-{buildVersion}"));
    var aitRunLink = Path.Combine(aitDirPath, "AppRun");
    if (File.Exists(aitRunLink)) File.Delete(aitRunLink);
    File.CreateSymbolicLink(Path.Combine(aitDirPath, "AppRun"),
        Path.Combine("opt", $"bple-{buildVersion}", $"BPLE-{buildVersion}.x86_64"));
    CopyTo(desktopPath, Path.Combine(aitDirPath, $"BPLE-{buildVersion}.desktop"));
    CopyTo(iconOrgPath, Path.Combine(aitDirPath, $"BPLE-{buildVersion}.png"));
    CopyTo(icon256Path, Path.Combine(aitDirPath, ".DirIcon"));
    CopyTo(icon512Path,
        Path.Combine(aitDirPath, "usr", "share", "icons", "hicolor", "512x512", "apps", $"BPLE-{buildVersion}.png"));

    var buildAi = Path.Combine(publishPath, $"BPLE-{buildVersion}-linux-{buildArchitecture}.AppImage");

    using (var process = Process.Start(new ProcessStartInfo
           {
               FileName = aitPath,
               ArgumentList = { aitDirPath, buildAi },
               UseShellExecute = false,
               CreateNoWindow = true,
               EnvironmentVariables =
               {
                   ["ARCH"] = buildArchitecture switch
                   {
                       "x64" => "x86_64",
                       _ => throw new UnreachableException()
                   }
               }
           }))
    {
        process?.WaitForExit();
        if (process?.ExitCode != 0)
        {
            Console.WriteLine($"appimagetool exited with non-zero exit code: {process?.ExitCode}");
        }
    }

    continue;

    void CopyForStaging(string source, string dest)
    {
        Directory.CreateDirectory(dest);

        foreach (var dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            if (dirPath.Contains("BackUpThisFolder_ButDontShipItWithYourGame")) continue;
            Directory.CreateDirectory(Path.Combine(dest, Path.GetRelativePath(source, dirPath)));
        }

        foreach (var filePath in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (filePath.Contains("BackUpThisFolder_ButDontShipItWithYourGame")) continue;
            var destFilePath = Path.Combine(dest, Path.GetRelativePath(source, filePath));
            File.Copy(filePath, destFilePath, true);

            if (Path.GetExtension(destFilePath) == ".x86_64")
            {
                File.SetUnixFileMode(destFilePath,
                    UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead |
                    UnixFileMode.UserWrite |
                    UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute
                );
            }
        }
    }

    void CopyFilesRecursively(string source, string dest)
    {
        foreach (var dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(dest, Path.GetRelativePath(source, dirPath)));

        foreach (var filePath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
            File.Copy(filePath, Path.Combine(dest, Path.GetRelativePath(source, filePath)), true);
    }

    void CopyTo(string sourcePath, string targetPath)
    {
        if (File.Exists(targetPath)) File.Delete(targetPath);
        var targetDir = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(targetDir);
        File.Copy(sourcePath, targetPath, true);
    }
}

return;

void DownloadFile(string url, string outFile, bool isExecutable = false)
{
    using (var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
    {
        response.EnsureSuccessStatusCode();

        using var httpStream = response.Content.ReadAsStream();
        using var fileStream = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None);
        httpStream.CopyTo(fileStream);
    }

    if (isExecutable)
    {
        File.SetUnixFileMode(outFile,
            UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute
        );
    }
}