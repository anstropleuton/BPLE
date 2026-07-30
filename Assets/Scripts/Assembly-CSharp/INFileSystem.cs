using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public static class INFileSystem
{
	private static string m_root;

	public static string Root
	{
		get
		{
			m_root = GetDefaultRoot();
			if (!string.IsNullOrEmpty(m_root) && !Directory.Exists(m_root))
			{
				Directory.CreateDirectory(m_root);
			}

			return m_root;
		}
	}

	private static string GetDefaultRoot()
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		using (AndroidJavaClass androidJavaClass = new AndroidJavaClass("android.os.Environment"))
		{
			AndroidJavaObject androidJavaObject =
 androidJavaClass.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", androidJavaClass.GetStatic<string>("DIRECTORY_DOCUMENTS"));
			if (androidJavaObject == null)
			{
				return string.Empty;
			}
			string text = androidJavaObject.Call<string>("getAbsolutePath");
			if (string.IsNullOrEmpty(text))
			{
				return string.Empty;
			}
			return Path.Combine(text, Application.productName);
		}
#elif UNITY_STANDALONE_LINUX
		string home = Environment.GetEnvironmentVariable("HOME");
		if (string.IsNullOrEmpty(home))
		{
			home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		}

		if (string.IsNullOrEmpty(home))
		{
			throw new PlatformNotSupportedException("Cannot find $HOME for user");
		}

		try
		{
			using var process = Process.Start(new ProcessStartInfo
			{
				FileName = "xdg-user-dir",
				Arguments = "DOCUMENTS",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			});

			string output = process.StandardOutput.ReadToEnd();
			process.WaitForExit();

			if (process.ExitCode == 0)
			{
				string path = output.Trim();
				if (!string.IsNullOrEmpty(path))
				{
					return path;
				}
			}
		}
		catch
		{
		}

		string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
		if (string.IsNullOrEmpty(configHome))
		{
			configHome = Path.Combine(home, ".config");
		}
		
		string configPath = Path.Combine(configHome, "user-dirs.dirs");
		if (File.Exists(configPath))
		{
			foreach (string line in File.ReadLines(configPath))
			{
				Match match = Regex.Match(line, @"^XDG_DOCUMENTS_DIR\s*=\s*""(.*)""\s*$");
				if (match.Success)
				{
					return match.Groups[1].Value.Replace("$HOME", home);
				}
			}
		}

		return Path.Combine(home, "Documents");
#elif UNITY_STANDALONE_WIN
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Application.productName);
#else
		throw new PlatformNotSupportedException("Update INFileSystem.cs to add support for other platform here");
#endif
	}
}