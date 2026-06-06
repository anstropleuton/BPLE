using System;
using System.IO;
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
			AndroidJavaObject androidJavaObject = androidJavaClass.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", androidJavaClass.GetStatic<string>("DIRECTORY_DOCUMENTS"));
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
#else
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Application.productName);
#endif
	}
}