using System;
using UnityEditor;
using UnityEngine;

public class AppVersionManager : EditorWindow
{
	private static string guiPendingVersion;

	[MenuItem("BPLE/Set Build Version")]
	public static void SetBuildVersion()
	{
		GetWindow(typeof(AppVersionManager), false, "Set Build Version");
	}

	[MenuItem("BPLE/Refresh App Id")]
	public static void RefreshAppId()
	{
		foreach (Orchestrate.Platform platform in Enum.GetValues(typeof(Orchestrate.Platform)))
		{
			PlayerSettings.companyName = "BPLE";
			PlayerSettings.productName = $"BPLE {PlayerSettings.bundleVersion}";
			PlayerSettings.SetApplicationIdentifier(
				BuildPipeline.GetBuildTargetGroup(Orchestrate.GetTargetFromPlatform(platform)),
				GetAppId(PlayerSettings.bundleVersion)
			);
		}
	}

	static string GetAppId(string version)
	{
		return $"com.bple.bple_{version.Replace('-', '_').Replace('.', '_')}";
	}

	void OnGUI()
	{
		if (guiPendingVersion == null)
			guiPendingVersion = PlayerSettings.bundleVersion;

		guiPendingVersion = EditorGUILayout.TextField("App version", guiPendingVersion);

		EditorGUILayout.HelpBox($"  App id will be {GetAppId(guiPendingVersion)}", MessageType.Info);

		GUILayout.FlexibleSpace();
		EditorGUILayout.BeginHorizontal();

		if (GUILayout.Button("Force Refresh"))
		{
			RefreshAppId();
		}

		GUI.enabled = guiPendingVersion != PlayerSettings.bundleVersion;
		if (GUILayout.Button("Apply"))
		{
			PlayerSettings.bundleVersion = guiPendingVersion;
			RefreshAppId();
		}
		GUI.enabled = true;

		EditorGUILayout.EndHorizontal();
	}
}