#!/usr/bin/env -S dotnet --
#:package Magick.NET-Q8-AnyCPU@14.15.0
#:package AssetsTools.NET@3.0.5

#:include Common.cs

using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using ImageMagick;

if (!OperatingSystem.IsLinux())
{
	Console.WriteLine("This script must be ran on Linux");
	Environment.Exit(1);
}

if (!Common.Initialize())
{
	Environment.Exit(1);
}

var iconOrgPath = Path.Combine(Common.AssetsPath, "Texture2D", "App Icon.png");

// 256x icon
var icon256Path = Path.Combine(Common.TempPath, "icon-256.png");

using (var image = new MagickImage(File.ReadAllBytes(iconOrgPath)))
{
	image.Resize(256, 256);
	image.Write(icon256Path, MagickFormat.Png);
}

// 512x icon
var icon512Path = Path.Combine(Common.TempPath, "icon-512.png");

using (var image = new MagickImage(File.ReadAllBytes(iconOrgPath)))
{
	image.Resize(512, 512);
	image.Write(icon512Path, MagickFormat.Png);
}

// Download tools

// nFPM
var nfpmArchivePath = Path.Combine(Common.TempPath, "nfpm.tar.gz");
if (!File.Exists(nfpmArchivePath))
{
	var nfpmUrl = "https://github.com/goreleaser/nfpm/releases/download/v2.47.0/nfpm_2.47.0_Linux_x86_64.tar.gz";
	if (!Common.DownloadFile(nfpmUrl, nfpmArchivePath))
	{
		Environment.Exit(1);
	}
}

var nfpmExtractPath = Path.Combine(Common.TempPath, "nfpm");
var nfpmBinPath = Path.Combine(nfpmExtractPath, "nfpm");
if (!Directory.Exists(nfpmExtractPath) || !File.Exists(nfpmBinPath))
{
	Directory.CreateDirectory(nfpmExtractPath);

	using var archiveStream = File.OpenRead(nfpmArchivePath);
	using var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress);

	TarFile.ExtractToDirectory(gzipStream, nfpmExtractPath, true);
}

// AppImageTool
var aitPath = Path.Combine(Common.TempPath, "ait.AppImage");
if (!File.Exists(aitPath))
{
	var aitUrl = "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage";
	if (!Common.DownloadFile(aitUrl, aitPath, true))
	{
		Environment.Exit(1);
	}
}

foreach (var (current, arch) in ((string, string)[])[("LinuxX64", "x64")])
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

	// Tar.gz
	var buildTar = Path.Combine(Common.PublishPath, $"BPLE-{Common.BuildVersion}-linux-{arch}.tar.gz");
	if (File.Exists(buildTar)) File.Delete(buildTar);
	Console.WriteLine($"Building {buildTar}");

	using (var fileStream = File.Create(buildTar))
	using (var gzipStream = new GZipStream(fileStream, CompressionMode.Compress))
	{
		TarFile.CreateFromDirectory(stagingPath, gzipStream, false);
	}

	// Packages
	var desktopPath = Path.Combine(Common.TempPath, $"BPLE-{Common.BuildVersion}.desktop");
	File.WriteAllText(desktopPath,
		$"""
			 [Desktop Entry]
			 Name=BPLE {Common.BuildVersion}
			 Comment=BPLE is a modification of the game Bad Piggies.
			 Exec=/opt/bple-{Common.BuildVersion}/BPLE-{Common.BuildVersion}.x86_64
			 Path=/opt/bple-{Common.BuildVersion}/
			 Icon=BPLE-{Common.BuildVersion}
			 Terminal=false
			 Type=Application
			 Categories=Game;
			 StartupWMClass=BPLE-{Common.BuildVersion}
			 """.Replace("\r\n", "\n"));

	var configPath = Path.Combine(Common.TempPath, "nfpm.yaml");
	File.WriteAllText(configPath,
		$"""
		 name: "bple-{Common.BuildVersion}"
		 arch: "amd64"
		 version: "{Common.BuildVersion.Replace('-', '.')}"
		 maintainer: "Anstro Pleuton"
		 description: "BPLE is a modification of the game Bad Piggies."
		 contents:
		   - src: "{stagingPath}/"
		     dst: "/opt/bple-{Common.BuildVersion}"
		     type: "tree"
		   - src: "/opt/bple-{Common.BuildVersion}/BPLE-{Common.BuildVersion}.x86_64"
		     dst: "/usr/bin/BPLE-{Common.BuildVersion}"
		     type: "symlink"
		   - src: "{desktopPath}"
		     dst: "/usr/share/applications/BPLE-{Common.BuildVersion}.desktop"
		   - src: "{icon512Path}"
		     dst: "/usr/share/icons/hicolor/512x512/apps/BPLE-{Common.BuildVersion}.png"
		 """);

	foreach (var (package, extension) in ((string, string)[])
	         [
		         ("archlinux", "pkg.tar.zst"), ("deb", "deb"), ("rpm", "rpm")
	         ])
	{
		var targetPath = Path.Combine(Common.PublishPath, $"BPLE-{Common.BuildVersion}-linux-{arch}.{extension}");
		Console.WriteLine($"Building {targetPath}");

		Environment.ExitCode += Common.RunCommand(nfpmBinPath,
			["package", "-f", configPath, "-p", package, "-t", targetPath]);
	}

	// AppImage
	var buildAi = Path.Combine(Common.PublishPath, $"BPLE-{Common.BuildVersion}-linux-{arch}.AppImage");
	Console.WriteLine($"Building {buildAi}");

	var requiredArch = arch switch
	{
		"x64" => "x86_64",
		_ => throw new UnreachableException()
	};

	var aitDirPath = Path.Combine(Common.TempPath, $"bple-{Common.BuildVersion}.AppDir");
	if (Directory.Exists(aitDirPath)) Directory.Delete(aitDirPath, true);

	Common.CopyFilesRecursively(stagingPath, Path.Combine(aitDirPath, "opt", $"bple-{Common.BuildVersion}"));
	var aitRunLink = Path.Combine(aitDirPath, "AppRun");
	if (File.Exists(aitRunLink)) File.Delete(aitRunLink);
	File.CreateSymbolicLink(Path.Combine(aitDirPath, "AppRun"),
		Path.Combine("opt", $"bple-{Common.BuildVersion}", $"BPLE-{Common.BuildVersion}.x86_64"));
	Common.CopyTo(desktopPath, Path.Combine(aitDirPath, $"BPLE-{Common.BuildVersion}.desktop"));
	Common.CopyTo(iconOrgPath, Path.Combine(aitDirPath, $"BPLE-{Common.BuildVersion}.png"));
	Common.CopyTo(icon256Path, Path.Combine(aitDirPath, ".DirIcon"));
	Common.CopyTo(icon512Path,
		Path.Combine(aitDirPath, "usr", "share", "icons", "hicolor", "512x512", "apps",
			$"BPLE-{Common.BuildVersion}.png"));

	Environment.ExitCode += Common.RunCommand(aitPath, [aitDirPath, buildAi],
		new Dictionary<string, string> { ["ARCH"] = requiredArch });
}