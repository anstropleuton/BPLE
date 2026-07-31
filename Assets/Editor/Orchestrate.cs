using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class Orchestrate
{
	public static readonly string ProjectPath = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/'));

	public static readonly string SourcePath = Path.Combine(ProjectPath, "Assets", "assetbundles");

	public static readonly string CachePath = Path.Combine(ProjectPath, "Library", "BuiltAssetBundles");

	public static readonly string StreamPath = Path.Combine(ProjectPath, "Assets", "StreamingAssets", "AssetBundles");

	public static readonly string BuildPath = Path.Combine(ProjectPath, "Builds");

	public static readonly string QueuePath = Path.Combine(ProjectPath, "Temp", "BuildQueue.txt");

	public static readonly List<BuildTarget> BuildTargets = new List<BuildTarget>()
	{
		BuildTarget.StandaloneWindows,
		BuildTarget.StandaloneWindows64,
		BuildTarget.StandaloneLinux64,
		BuildTarget.Android
	};

	static void PushQueue(BuildTarget target)
	{
		File.AppendAllText(QueuePath, target.ToString() + "\n");
	}

	static BuildTarget? NextQueued()
	{
		if (!File.Exists(QueuePath)) return null;

		string[] lines = File.ReadAllLines(QueuePath);

		if (lines.Length == 0) return null;

		return Enum.Parse<BuildTarget>(lines[0]);
	}

	static void PopQueue()
	{
		if (!File.Exists(QueuePath)) return;

		string[] lines = File.ReadAllLines(QueuePath);

		if (lines.Length == 0) return;

		File.WriteAllLines(QueuePath, lines.Skip(1));
	}

	// https://discussions.unity.com/t/buildpipeline-buildplayer-wont-load-sysroot-toolchain-packages/826597/6
	[InitializeOnLoadMethod]
	static void CheckBuildOnLoad()
	{
		BuildTarget? target = NextQueued();
		if (target == null) return;
		
		PopQueue();

		switch (target)
		{
			case BuildTarget.StandaloneWindows:
				EditorApplication.delayCall += ContinueBuildingStandaloneWindows;
				break;
			case BuildTarget.StandaloneWindows64:
				EditorApplication.delayCall += ContinueBuildingStandaloneWindows64;
				break;
			case BuildTarget.StandaloneLinux64:
				EditorApplication.delayCall += ContinueBuildingStandaloneLinux64;
				break;
			case BuildTarget.Android:
				EditorApplication.delayCall += ContinueBuildingAndroid;
				break;
		}
	}

	static void ContinueBuildingStandaloneWindows()
	{
		EditorApplication.delayCall -= ContinueBuildingStandaloneWindows;
		ContinueBuilding(BuildTarget.StandaloneWindows);
	}

	static void ContinueBuildingStandaloneWindows64()
	{
		EditorApplication.delayCall -= ContinueBuildingStandaloneWindows64;
		ContinueBuilding(BuildTarget.StandaloneWindows64);
	}

	static void ContinueBuildingStandaloneLinux64()
	{
		EditorApplication.delayCall -= ContinueBuildingStandaloneLinux64;
		ContinueBuilding(BuildTarget.StandaloneLinux64);
	}

	static void ContinueBuildingAndroid()
	{
		EditorApplication.delayCall -= ContinueBuildingAndroid;
		ContinueBuilding(BuildTarget.Android);
	}

	[MenuItem("BPLE/Build/All")]
	public static void BuildAll()
	{
		foreach (BuildTarget target in BuildTargets)
		{
			BuildForTarget(target);
		}
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Windows x32")]
	public static void BuildWindowsX86()
	{
		BuildForTarget(BuildTarget.StandaloneWindows);
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Windows x64")]
	public static void BuildWindowsX64()
	{
		BuildForTarget(BuildTarget.StandaloneWindows64);
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Linux")]
	public static void BuildLinux()
	{
		BuildForTarget(BuildTarget.StandaloneLinux64);
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Android")]
	public static void BuildAndroid()
	{
		BuildForTarget(BuildTarget.Android);
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Bundle/All")]
	public static void BundleAll()
	{
		foreach (BuildTarget target in BuildTargets)
		{
			BundleForTarget(target);
		}
	}

	[MenuItem("BPLE/Bundle/Windows x32")]
	public static void BundleWindowsX86()
	{
		BundleForTarget(BuildTarget.StandaloneWindows);
	}

	[MenuItem("BPLE/Bundle/Windows x64")]
	public static void BundleWindowsX64()
	{
		BundleForTarget(BuildTarget.StandaloneWindows64);
	}

	[MenuItem("BPLE/Bundle/Linux")]
	public static void BundleLinux()
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
		Directory.Delete(CachePath, true);
		Directory.Delete(StreamPath, true);
		File.Delete(StreamPath + ".meta");
	}

	static void ProcessQueuedBuilds()
	{
		BuildTarget? target = NextQueued();
		if (target == null) return;

		EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target.Value), target.Value);
	}

	static void BuildForTarget(BuildTarget target)
	{
		Debug.Log($"Source path: {SourcePath}");
		Debug.Log($"Cache path: {CachePath}");
		Debug.Log($"Stream path: {StreamPath}");
		Debug.Log($"Build path: {BuildPath}");

		Debug.Log($"Building for: {target}");

		string targetPath = Path.Combine(BuildPath, target.ToString());
		if (Directory.Exists(targetPath)) Directory.Delete(targetPath, true);
		Directory.CreateDirectory(targetPath);

		BundleForTarget(target);

		if (EditorUserBuildSettings.activeBuildTarget != target)
		{
			Debug.Log("Switching platform requires domain reload.");
			PushQueue(target);
		}
		else
		{
			ContinueBuilding(target);
		}
	}

	static void ContinueBuilding(BuildTarget target)
	{
		if (Directory.Exists(StreamPath))
		{
			Directory.Delete(StreamPath, true);
			File.Delete(StreamPath + ".meta");
		}

		Directory.CreateDirectory(StreamPath);
		CopyFilesRecursively(Path.Combine(CachePath, GetAssetFolder(target)), StreamPath);

		string[] scenes = EditorBuildSettings.scenes.Select(scene => scene.path).ToArray();
		if (scenes.Length == 0)
			throw new Exception("No scenes in build settings");

		BuildPlayerOptions options = new BuildPlayerOptions
		{
			scenes = scenes,
			locationPathName = Path.Combine(BuildPath, target.ToString(), GetBinary(target)),
			target = target
		};

		// BuildReport report = BuildPipeline.BuildPlayer(options);
		// if (report.summary.result != BuildResult.Succeeded)
		// 	throw new Exception($"Build failed with errors: {report.summary.totalErrors}");

		Debug.Log($"Built game: {options.locationPathName}");

		Directory.Delete(StreamPath, true);
		File.Delete(StreamPath + ".meta");

		ProcessQueuedBuilds();
	}

	static void BundleForTarget(BuildTarget target)
	{
		Debug.Log($"Source path: {SourcePath}");
		Debug.Log($"Cache path: {CachePath}");
		Debug.Log($"Stream path: {StreamPath}");
		Debug.Log($"Build path: {BuildPath}");

		Debug.Log($"Bundling for {target}");

		string targetPath = Path.Combine(CachePath, GetAssetFolder(target));
		if (Directory.Exists(targetPath))
		{
			Debug.Log("Assets already exists. If refreshing is needed, use BPLE > Bundle > Clear");
			return;
		}

		Directory.CreateDirectory(targetPath);

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
			importer.name = Path.GetFileName(Path.GetDirectoryName(path));

			Debug.Log($"Imported asset: {importer.name} => {path}");
		}

		AssetDatabase.RemoveUnusedAssetBundleNames();
		AssetDatabase.SaveAssets();

		BuildPipeline.BuildAssetBundles(targetPath, BuildAssetBundleOptions.ChunkBasedCompression, target);
		Debug.Log($"Built asset bundle: {targetPath}");
	}

	static string GetAssetFolder(BuildTarget target)
	{
		return target switch
		{
			BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneWindows => "Windows",
			BuildTarget.StandaloneLinux64 => "Linux",
			BuildTarget.Android => "Android",
			_ => null,
		};
	}

	static string GetBinary(BuildTarget target)
	{
		return target switch
		{
			BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneWindows => $"BPLE-{Application.version}.exe",
			BuildTarget.StandaloneLinux64 => $"BPLE-{Application.version}.x86_64",
			BuildTarget.Android => $"BPLE-{Application.version}.apk",
			_ => null,
		};
	}

	static void CopyFilesRecursively(string source, string dest)
	{
		foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
			Directory.CreateDirectory(Path.Combine(dest, Path.GetRelativePath(source, dirPath)));

		foreach (string filePath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
			File.Copy(filePath, Path.Combine(dest, Path.GetRelativePath(source, filePath)), true);
	}
}