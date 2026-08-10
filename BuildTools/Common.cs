using System.Diagnostics;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

internal static class Common
{
	private static readonly HttpClient Client = new();

	public static string ToolsPath = null!;

	public static string AssetsPath = null!;

	public static string ToolsDataPath = null!;

	public static string BuildsPath = null!;

	public static string PublishPath = null!;

	public static string TempPath = null!;

	public static string BuildVersion = null!;

	public static bool Initialize()
	{
		if (Path.GetFileName(Directory.GetCurrentDirectory()) == "BuildTools")
		{
			Directory.SetCurrentDirectory(Path.GetDirectoryName(Directory.GetCurrentDirectory())!);
		}

		if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Assets")))
		{
			Console.WriteLine("This script must be ran in BPLE or BPLE/BuildTools directory");
			return false;
		}

		ToolsPath = Path.Combine(Directory.GetCurrentDirectory(), "BuildTools");
		AssetsPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets");

		ToolsDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"BPLE-Build-Tools");
		Directory.CreateDirectory(ToolsDataPath);

		BuildsPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
		Directory.CreateDirectory(BuildsPath);

		PublishPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Publish");
		Directory.CreateDirectory(PublishPath);

		TempPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "Publisher");
		Directory.CreateDirectory(TempPath);

		var psPath = Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "ProjectSettings.asset");

		var versionLine = File.ReadLines(psPath).FirstOrDefault(line => line.Contains("bundleVersion"));
		if (versionLine == null)
		{
			Console.WriteLine("Could not find bundleVersion in ProjectSettings.asset");
			return false;
		}

		BuildVersion = versionLine.Split(':', 2)[1].Trim();

		return true;
	}

	public static bool DownloadFile(string url, string outFile, bool isExecutable = false)
	{
		var name = Path.GetFileName(outFile);
		Console.WriteLine($"Downloading {name} (from {url})");

		try
		{
			using var response = Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter()
				.GetResult();
			response.EnsureSuccessStatusCode();

			using var httpStream = response.Content.ReadAsStream();
			using var fileStream = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None);
			httpStream.CopyTo(fileStream);
		}
		catch (Exception exception)
		{
			Console.WriteLine($"Failed to download {name}: {exception.Message}");
			return false;
		}

		if (isExecutable && OperatingSystem.IsLinux())
		{
			File.SetUnixFileMode(outFile,
				UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead |
				UnixFileMode.UserWrite |
				UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute
			);
		}

		return true;
	}

	public static int RunCommand(string path, string[] args, Dictionary<string, string>? envs = null)
	{
		var name = Path.GetFileName(path);
		Console.WriteLine($"Running {name} {string.Join(' ', args)}");

		var info = new ProcessStartInfo
		{
			FileName = path,
			UseShellExecute = false
		};

		foreach (var arg in args)
		{
			info.ArgumentList.Add(arg);
		}

		if (envs != null)
		{
			foreach (var (env, value) in envs)
			{
				info.EnvironmentVariables[env] = value;
			}
		}

		using var process = Process.Start(info);

		if (process == null)
		{
			Console.WriteLine($"{name} did not run");
			return 1;
		}

		process.WaitForExit();

		if (process.ExitCode != 0)
		{
			Console.WriteLine($"{name} exited with non-zero exit code: {process.ExitCode}");
		}

		return process.ExitCode;
	}

	public static void CopyFilesRecursively(string source, string dest)
	{
		foreach (var dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
			Directory.CreateDirectory(Path.Combine(dest, Path.GetRelativePath(source, dirPath)));

		foreach (var filePath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
			File.Copy(filePath, Path.Combine(dest, Path.GetRelativePath(source, filePath)), true);
	}

	public static void CopyTo(string sourcePath, string targetPath)
	{
		if (File.Exists(targetPath)) File.Delete(targetPath);
		var targetDir = Path.GetDirectoryName(targetPath)!;
		Directory.CreateDirectory(targetDir);
		File.Copy(sourcePath, targetPath, true);
	}

	public static void StageBuild(string buildPath, string stagingPath)
	{
		if (Directory.Exists(stagingPath)) Directory.Delete(stagingPath, true);
		Directory.CreateDirectory(stagingPath);

		Directory.CreateDirectory(stagingPath);

		foreach (var dirPath in Directory.GetDirectories(buildPath, "*", SearchOption.AllDirectories))
		{
			if (dirPath.Contains("BackUpThisFolder_ButDontShipItWithYourGame")) continue;
			Directory.CreateDirectory(Path.Combine(stagingPath, Path.GetRelativePath(buildPath, dirPath)));
		}

		foreach (var filePath in Directory.GetFiles(buildPath, "*", SearchOption.AllDirectories))
		{
			if (filePath.Contains("BackUpThisFolder_ButDontShipItWithYourGame")) continue;
			var destFilePath = Path.Combine(stagingPath, Path.GetRelativePath(buildPath, filePath));
			File.Copy(filePath, destFilePath, true);

			if (OperatingSystem.IsLinux() && Path.GetExtension(destFilePath) == ".x86_64")
			{
				File.SetUnixFileMode(destFilePath,
					UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead |
					UnixFileMode.UserWrite |
					UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute
				);
			}
		}
	}

	public static void PatchGgm(string ggmPath)
	{
		var orgGgmPath = ggmPath + ".original";
		if (!File.Exists(orgGgmPath)) File.Copy(ggmPath, orgGgmPath);

		var manager = new AssetsManager();
		manager.LoadClassPackage(Path.Combine(ToolsPath, "uncompressed.tpk"));

		var assetInfo = manager.LoadAssetsFile(orgGgmPath);
		var assetFile = assetInfo.file;

		manager.LoadClassDatabaseFromPackage(assetFile.Metadata.UnityVersion);

		var psInfo = assetFile.GetAssetsOfType(AssetClassID.PlayerSettings)[0];
		var psData = manager.GetBaseField(assetInfo, psInfo);

		psData["m_ShowUnitySplashScreen"].AsBool = false;
		psData["m_ShowUnitySplashLogo"].AsBool = false;

		psInfo.SetNewData(psData);

		var bsInfo = assetFile.GetAssetsOfType(AssetClassID.BuildSettings)[0];
		var bsData = manager.GetBaseField(assetInfo, bsInfo);

		bsData["hasPROVersion"].AsBool = true;

		bsInfo.SetNewData(bsData);

		using var writer = new AssetsFileWriter(ggmPath);
		assetFile.Write(writer);
	}
}