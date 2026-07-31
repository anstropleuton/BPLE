using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class Orchestrate
{
	public static readonly string
		ProjectPath = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/'));

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

	static void ClearQueue()
	{
		File.WriteAllLines(QueuePath, new string[] { });
	}

	// https://discussions.unity.com/t/buildpipeline-buildplayer-wont-load-sysroot-toolchain-packages/826597/6
	[InitializeOnLoadMethod]
	static void CheckBuildOnLoad()
	{
		BuildTarget? target = NextQueued();
		if (target == null) return;

		switch (target)
		{
			case BuildTarget.StandaloneWindows:
				EditorApplication.delayCall += CompileStandaloneWindows;
				break;
			case BuildTarget.StandaloneWindows64:
				EditorApplication.delayCall += CompileStandaloneWindows64;
				break;
			case BuildTarget.StandaloneLinux64:
				EditorApplication.delayCall += CompileStandaloneLinux64;
				break;
			case BuildTarget.Android:
				EditorApplication.delayCall += CompileAndroid;
				break;
		}
	}

	static void CompileStandaloneWindows()
	{
		EditorApplication.delayCall -= CompileStandaloneWindows;
		CompileTarget(BuildTarget.StandaloneWindows);
		PopQueue();
		ProcessQueuedBuilds();
	}

	static void CompileStandaloneWindows64()
	{
		EditorApplication.delayCall -= CompileStandaloneWindows64;
		CompileTarget(BuildTarget.StandaloneWindows64);
		PopQueue();
		ProcessQueuedBuilds();
	}

	static void CompileStandaloneLinux64()
	{
		EditorApplication.delayCall -= CompileStandaloneLinux64;
		CompileTarget(BuildTarget.StandaloneLinux64);
		PopQueue();
		ProcessQueuedBuilds();
	}

	static void CompileAndroid()
	{
		EditorApplication.delayCall -= CompileAndroid;
		CompileTarget(BuildTarget.Android);
		PopQueue();
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/All")]
	public static void BuildAll()
	{
		ClearQueue();

		foreach (BuildTarget target in BuildTargets)
		{
			BundleForTarget(target);
			PushQueue(target);
		}

		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Windows x32")]
	public static void BuildWindowsX86()
	{
		ClearQueue();
		BundleForTarget(BuildTarget.StandaloneWindows);
		PushQueue(BuildTarget.StandaloneWindows);
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Windows x64")]
	public static void BuildWindowsX64()
	{
		ClearQueue();
		BundleForTarget(BuildTarget.StandaloneWindows64);
		PushQueue(BuildTarget.StandaloneWindows64);
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Linux")]
	public static void BuildLinux()
	{
		ClearQueue();
		BundleForTarget(BuildTarget.StandaloneLinux64);
		PushQueue(BuildTarget.StandaloneLinux64);
		ProcessQueuedBuilds();
	}

	[MenuItem("BPLE/Build/Android")]
	public static void BuildAndroid()
	{
		ClearQueue();
		BundleForTarget(BuildTarget.Android);
		PushQueue(BuildTarget.Android);
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

		if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
			    BuildPipeline.GetBuildTargetGroup(target.Value), target.Value))
		{
			ClearQueue();
			throw new Exception($"Failed to switch active build target to {target}");
		}

		EditorUtility.RequestScriptReload();
	}

	static void CompileTarget(BuildTarget target)
	{
		if (Directory.Exists(StreamPath))
		{
			Directory.Delete(StreamPath, true);
			File.Delete(StreamPath + ".meta");
		}

		Directory.CreateDirectory(StreamPath);
		CopyFilesRecursively(Path.Combine(CachePath, GetAssetFolder(target)), StreamPath);

		string[] scenes = EditorBuildSettings.scenes
			.Where(scene => scene.enabled)
			.Select(scene => scene.path)
			.ToArray();

		if (scenes.Length == 0)
		{
			ClearQueue();
			throw new Exception("No scenes in build settings");
		}

		string targetPath = Path.Combine(BuildPath, target.ToString());
		if (Directory.Exists(targetPath)) Directory.Delete(targetPath, true);
		Directory.CreateDirectory(targetPath);

		BuildPlayerOptions options = new BuildPlayerOptions
		{
			scenes = scenes,
			locationPathName = Path.Combine(targetPath, GetBinary(target)),
			target = target
		};

		BuildReport report = BuildPipeline.BuildPlayer(options);
		if (report.summary.result != BuildResult.Succeeded)
		{
			ClearQueue();
			throw new Exception($"Build failed with errors: {report.summary.totalErrors}");
		}

		Debug.Log($"Built game: {options.locationPathName}");

		Directory.Delete(StreamPath, true);
		File.Delete(StreamPath + ".meta");
	}

	static void BundleForTarget(BuildTarget target)
	{
		string targetPath = Path.Combine(CachePath, GetAssetFolder(target));
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