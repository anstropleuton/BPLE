#:package Magick.NET-Q8-AnyCPU@14.15.0

using System.Diagnostics;
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

// 256x icon .ico
var iconIcoPath = Path.Combine(tempPath, "icon.ico");

using (var image = new MagickImage(File.ReadAllBytes(iconOrgPath)))
{
	image.Resize(256, 256);
	image.Write(iconIcoPath, MagickFormat.Ico);
}

// Download tools
using var client = new HttpClient();

// Inno Setup itself has only installer released
// Extract it using Inno Unpacker
var ipArchivePath = Path.Combine(tempPath, "ip.zip");
if (!File.Exists(ipArchivePath))
{
	var ipUrl = "https://rathlev-home.de/tools/download/innounpacker.zip";
	Console.WriteLine($"Downloading {ipUrl}");
	DownloadFile(ipUrl, ipArchivePath);
}

var ipExtractPath = Path.Combine(tempPath, "ip");
var ipBinPath = Path.Combine(ipExtractPath, "innounp.exe");
if (!Directory.Exists(ipExtractPath) || !File.Exists(ipBinPath))
{
	Directory.CreateDirectory(ipExtractPath);

	ZipFile.ExtractToDirectory(ipArchivePath, ipExtractPath, true);
}

// Inno Setup
var isArchivePath = Path.Combine(tempPath, "is.exe");
if (!File.Exists(isArchivePath))
{
	var isUrl = "https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe";
	Console.WriteLine($"Downloading {isUrl}");
	DownloadFile(isUrl, isArchivePath);
}

var isExtractPath = Path.Combine(tempPath, "is");
var isBinPath = Path.Combine(isExtractPath, "{app}", "ISCC.exe");
if (!Directory.Exists(isExtractPath) || !File.Exists(isBinPath))
{
	using var process = Process.Start(new ProcessStartInfo
	{
		FileName = ipBinPath,
		ArgumentList = { "-x", "-b", $"-d{isExtractPath}", "-a", "-y", isArchivePath },
		UseShellExecute = false,
		CreateNoWindow = true
	});
	process?.WaitForExit();
	if (process?.ExitCode != 0)
	{
		Console.WriteLine($"Inno Unpacker exited with non-zero exit code: {process?.ExitCode}");
		return;
	}
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
	var allowedArch = buildArchitecture switch
	{
		"x64" => "x64compatible",
		"x32" => "x86compatible",
		_ => throw new UnreachableException()
	};

	var imageFile = Path.Combine(Directory.GetCurrentDirectory(), "BuildTools", "Background.png");
	var smallImageFile = Path.Combine(Directory.GetCurrentDirectory(), "BuildTools", "Icon.png");

	var scriptPath = Path.Combine(tempPath, "installer.iss");
	File.WriteAllText(scriptPath,
		$$"""
		  [Setup]
		  AppId=BPLE-{{buildVersion}}
		  AppName=BPLE {{buildVersion}}
		  AppVersion={{buildVersion}}
		  AppPublisher=Anstro Pleuton
		  AppComments=BPLE is a modification of the game Bad Piggies.

		  PrivilegesRequired=lowest
		  PrivilegesRequiredOverridesAllowed=dialog commandline

		  DefaultDirName={autopf}\BPLE {{buildVersion}}
		  DefaultGroupName=BPLE
		  AllowNoIcons=yes
		  AllowRootDirectory=yes

		  SolidCompression=yes
		  OutputDir={{publishPath}}
		  OutputBaseFilename=BPLE-{{buildVersion}}-windows-{{buildArchitecture}}

		  SetupIconFile={{iconIcoPath}}
		  ArchitecturesAllowed={{allowedArch}}

		  WizardStyle=modern dynamic windows11
		  WizardImageFile={{imageFile}}
		  WizardSmallImageFile={{smallImageFile}}

		  UninstallDisplayIcon={app}\BPLE-{{buildVersion}}.exe

		  [Files]
		  Source: "{{stagingPath}}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

		  [Icons]
		  Name: "{group}\BPLE {{buildVersion}}"; Filename: "{app}\BPLE-{{buildVersion}}.exe"
		  Name: "{group}\Uninstall BPLE {{buildVersion}}"; Filename: "{uninstallexe}"
		  Name: "{autodesktop}\BPLE {{buildVersion}}"; Filename: "{app}\BPLE-{{buildVersion}}.exe"; Tasks: desktopicon
		  """);

	using (var process = Process.Start(new ProcessStartInfo
	       {
		       FileName = isBinPath,
		       ArgumentList = { scriptPath }
	       }))
	{
		process?.WaitForExit();
		if (process?.ExitCode != 0)
		{
			Console.WriteLine($"Inno Setup compiler exited with non-zero exit code: {process?.ExitCode}");
		}
	}

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
