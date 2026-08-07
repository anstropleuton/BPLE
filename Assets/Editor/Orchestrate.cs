using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class Orchestrate
{
	public static readonly string ProjectPath =
		Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/'));

	public static readonly string SourcePath = Path.Combine(ProjectPath, "Assets", "assetbundles");

	public static readonly string CachePath = Path.Combine(ProjectPath, "Library", "BuiltAssetBundles");

	public static readonly string StreamPath = Path.Combine(ProjectPath, "Assets", "StreamingAssets", "AssetBundles");

	public static readonly string BuildPath = Path.Combine(ProjectPath, "Builds");

	public static readonly string QueuePath = Path.Combine(ProjectPath, "Temp", "BuildQueue.txt");

	public enum Platform
	{
		WindowsX32,
		WindowsX64,
		LinuxX64,
		AndroidArm,
		AndroidIntel
	}

	public static BuildTarget GetTargetFromPlatform(Platform platform)
	{
		return platform switch
		{
			Platform.WindowsX32 => BuildTarget.StandaloneWindows,
			Platform.WindowsX64 => BuildTarget.StandaloneWindows64,
			Platform.LinuxX64 => BuildTarget.StandaloneLinux64,
			Platform.AndroidArm or Platform.AndroidIntel => BuildTarget.Android,
			_ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported target")
		};
	}

	public static string GetBinaryFromPlatform(Platform platform)
	{
		return platform switch
		{
			Platform.WindowsX32 or Platform.WindowsX64 => $"BPLE-{PlayerSettings.bundleVersion}.exe",
			Platform.LinuxX64 => $"BPLE-{PlayerSettings.bundleVersion}.x86_64",
			Platform.AndroidArm or Platform.AndroidIntel => $"BPLE-{PlayerSettings.bundleVersion}.apk",
			_ => null,
		};
	}

	static void PushQueue(Platform platform)
	{
		File.AppendAllText(QueuePath, $"{platform}\n");
	}

	static Platform? NextQueued()
	{
		if (!File.Exists(QueuePath)) return null;

		string[] lines = File.ReadAllLines(QueuePath);

		if (lines.Length == 0) return null;

		return Enum.Parse<Platform>(lines[0]);
	}

	static void PopQueue()
	{
		if (!File.Exists(QueuePath)) return;

		string[] lines = File.ReadAllLines(QueuePath);

		if (lines.Length == 0) return;

		File.WriteAllLines(QueuePath, lines.Skip(1));
	}

	static void ClearQueue()
	{
		File.WriteAllLines(QueuePath, new string[] { });
	}

	// https://discussions.unity.com/t/buildpipeline-buildplayer-wont-load-sysroot-toolchain-packages/826597/6
	private static EditorApplication.CallbackFunction pendingBuild;

	[InitializeOnLoadMethod]
	static void CheckBuildOnLoad()
	{
		Platform? platform = NextQueued();
		if (platform == null) return;

		pendingBuild = () =>
		{
			EditorApplication.delayCall -= pendingBuild;
			BuildForPlatform(platform.Value);
			PopQueue();
			ProcessQueuedBuilds();
		};
		EditorApplication.delayCall += pendingBuild;
	}

	[MenuItem("BPLE/Build/All")]
	public static void BuildAll()
	{
		ClearQueue();

		foreach (Platform platform in Enum.GetValues(typeof(Platform)))
		{
			BundleForTarget(GetTargetFromPlatform(platform));
			PushQueue(platform);
		}

		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Windows x32")]
	public static void BuildWindowsX86()
	{
		ClearQueue();
		BundleForTarget(BuildTarget.StandaloneWindows);
		PushQueue(Platform.WindowsX32);
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Windows x64")]
	public static void BuildWindowsX64()
	{
		ClearQueue();
		BundleForTarget(BuildTarget.StandaloneWindows64);
		PushQueue(Platform.WindowsX64);
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Linux x64")]
	public static void BuildLinux()
	{
		ClearQueue();
		BundleForTarget(BuildTarget.StandaloneLinux64);
		PushQueue(Platform.LinuxX64);
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Android ARM")]
	public static void BuildAndroidArm()
	{
		ClearQueue();
		BundleForTarget(BuildTarget.Android);
		PushQueue(Platform.AndroidArm);
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Android Intel")]
	public static void BuildAndroidIntel()
	{
		ClearQueue();
		BundleForTarget(BuildTarget.Android);
		PushQueue(Platform.AndroidIntel);
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Clear")]
	public static void BuildClear()
	{
		foreach (Platform platform in Enum.GetValues(typeof(Platform)))
		{
			string path = Path.Combine(BuildPath, platform.ToString());
			if (Directory.Exists(path)) Directory.Delete(path, true);
		}
	}

	[MenuItem("BPLE/Bundle/All")]
	public static void BundleAll()
	{
		foreach (Platform platform in Enum.GetValues(typeof(Platform)))
		{
			BundleForTarget(GetTargetFromPlatform(platform));
		}
	}

	[MenuItem("BPLE/Bundle/Windows x32")]
	public static void BundleWindowsX32()
	{
		BundleForTarget(BuildTarget.StandaloneWindows);
	}

	[MenuItem("BPLE/Bundle/Windows x64")]
	public static void BundleWindowsX64()
	{
		BundleForTarget(BuildTarget.StandaloneWindows64);
	}

	[MenuItem("BPLE/Bundle/Linux x64")]
	public static void BundleLinuxX64()
	{
		BundleForTarget(BuildTarget.StandaloneLinux64);
	}

	[MenuItem("BPLE/Bundle/Android")]
	public static void BundleAndroid()
	{
		BundleForTarget(BuildTarget.Android);
	}

	[MenuItem("BPLE/Bundle/Clear")]
	public static void BundleClear()
	{
		if (Directory.Exists(CachePath)) Directory.Delete(CachePath, true);
		if (Directory.Exists(StreamPath)) Directory.Delete(StreamPath, true);
		if (File.Exists(StreamPath + ".meta")) File.Delete(StreamPath + ".meta");
	}

	static void ProcessQueuedBuilds()
	{
		Platform? platform = NextQueued();
		if (platform == null) return;

		BuildTarget target = GetTargetFromPlatform(platform.Value);

		if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target))
		{
			ClearQueue();
			throw new Exception($"Failed to switch active build target to {target}");
		}

		EditorUtility.RequestScriptReload();
	}

	static void BuildForPlatform(Platform platform)
	{
		if (Directory.Exists(StreamPath))
		{
			Directory.Delete(StreamPath, true);
			File.Delete(StreamPath + ".meta");
		}

		BuildTarget target = GetTargetFromPlatform(platform);

		Directory.CreateDirectory(StreamPath);
		CopyFilesRecursively(Path.Combine(CachePath, target.ToString()), StreamPath);

		string[] scenes = EditorBuildSettings.scenes
			.Where(scene => scene.enabled)
			.Select(scene => scene.path)
			.ToArray();

		if (scenes.Length == 0)
		{
			ClearQueue();
			throw new Exception("No scenes in build settings");
		}

		string targetPath = Path.Combine(BuildPath, platform.ToString());
		if (Directory.Exists(targetPath)) Directory.Delete(targetPath, true);
		Directory.CreateDirectory(targetPath);

		BuildPlayerOptions options = new BuildPlayerOptions
		{
			scenes = scenes,
			locationPathName = Path.Combine(targetPath, GetBinaryFromPlatform(platform)),
			target = target
		};

		switch (platform)
		{
			case Platform.AndroidArm:
				PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
				break;
			case Platform.AndroidIntel:
				PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86 | AndroidArchitecture.X86_64;
				break;
		}

		BuildReport report = BuildPipeline.BuildPlayer(options);
		if (report.summary.result != BuildResult.Succeeded)
		{
			ClearQueue();
			throw new Exception($"Build failed with errors: {report.summary.totalErrors}:\n{report.steps}");
		}

		Debug.Log($"Built game: {options.locationPathName}");

		Directory.Delete(StreamPath, true);
		File.Delete(StreamPath + ".meta");
	}

	static void BundleForTarget(BuildTarget target)
	{
		string targetPath = Path.Combine(CachePath, target.ToString());
		if (Directory.Exists(targetPath))
		{
			Debug.Log("Assets already exists. If refreshing is needed, use BPLE > Bundle > Clear");
			return;
		}

		string[] guids = AssetDatabase.FindAssets(string.Empty, new[]
		{
			Path.GetRelativePath(ProjectPath, SourcePath)
		});

		if (guids.Length == 0)
			throw new Exception($"No assets in: {SourcePath}");

		foreach (string guid in guids)
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);

			if (AssetDatabase.IsValidFolder(path)) continue;

			AssetImporter importer = AssetImporter.GetAtPath(path);
			importer.assetBundleName = Path.GetFileName(Path.GetDirectoryName(path));

			Debug.Log($"Imported asset: {importer.assetBundleName} => {path}");
		}

		AssetDatabase.RemoveUnusedAssetBundleNames();
		AssetDatabase.SaveAssets();

		Directory.CreateDirectory(targetPath);

		BuildPipeline.BuildAssetBundles(targetPath, BuildAssetBundleOptions.ChunkBasedCompression, target);
		Debug.Log($"Built asset bundle: {targetPath}");
	}

	static void CopyFilesRecursively(string source, string dest)
	{
		foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
			Directory.CreateDirectory(Path.Combine(dest, Path.GetRelativePath(source, dirPath)));

		foreach (string filePath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
			File.Copy(filePath, Path.Combine(dest, Path.GetRelativePath(source, filePath)), true);
	}
}