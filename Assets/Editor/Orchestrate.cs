using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class Orchestrate
{
    static readonly string ProjectPath = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/'));

    static readonly string SourcePath = Path.Combine(ProjectPath, "Assets", "assetbundles");

    static readonly string CachePath = Path.Combine(ProjectPath, "Library", "BuiltAssetBundles");

    static readonly string StreamPath = Path.Combine(ProjectPath, "Assets", "StreamingAssets", "AssetBundles");
    
    static readonly string BuildPath = Path.Combine(ProjectPath, "Builds");

    static readonly List<BuildTarget> BuildTargets = new List<BuildTarget>()
    {
        BuildTarget.StandaloneWindows64,
        BuildTarget.StandaloneWindows,
        BuildTarget.StandaloneLinux64,
        BuildTarget.Android
    };

    [MenuItem("BPLE/Build/All")]
    public static void BuildAll()
    {
        foreach (BuildTarget target in BuildTargets)
        {
            BuildForTarget(target);
        }
    }

    [MenuItem("BPLE/Build/Windows x86")]
    public static void BuildWindowsX86()
    {
        BuildForTarget(BuildTarget.StandaloneWindows);
    }

    [MenuItem("BPLE/Build/Windows x64")]
    public static void BuildWindowsX64()
    {
        BuildForTarget(BuildTarget.StandaloneWindows64);
    }

    [MenuItem("BPLE/Build/Linux")]
    public static void BuildLinux()
    {
        BuildForTarget(BuildTarget.StandaloneLinux64);
    }

    [MenuItem("BPLE/Build/Android")]
    public static void BuildAndroid()
    {
        BuildForTarget(BuildTarget.Android);
    }

    [MenuItem("BPLE/Bundle/All")]
    public static void BundleAll()
    {
        foreach (BuildTarget target in BuildTargets)
        {
            BundleForTarget(target);
        }
    }

    [MenuItem("BPLE/Bundle/Windows x64")]
    public static void BundleWindowsX64()
    {
        BundleForTarget(BuildTarget.StandaloneWindows64);
    }

    [MenuItem("BPLE/Bundle/Windows x86")]
    public static void BundleWindowsX86()
    {
        BundleForTarget(BuildTarget.StandaloneWindows);
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

        BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(target);

        BuildPlayerOptions options = new BuildPlayerOptions()
        {
            scenes = scenes,
            locationPathName = Path.Combine(targetPath, GetBinary(target)),
            target = target,
            targetGroup = targetGroup,
        };

        BuildTarget previousTarget = EditorUserBuildSettings.activeBuildTarget;

        EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target);
        
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception($"Build failed with errors: {report.summary.totalErrors}");

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(previousTarget),
            previousTarget);

        Debug.Log($"Built game: {targetPath}");
        
        Directory.Delete(StreamPath, true);
        File.Delete(StreamPath + ".meta");
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
        
        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { Path.GetRelativePath(ProjectPath, SourcePath) });
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
        foreach (var dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(dest, Path.GetRelativePath(source, dirPath)));

        foreach (var filePath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
            File.Copy(filePath, Path.Combine(dest, Path.GetRelativePath(source, filePath)), true);
    }
}