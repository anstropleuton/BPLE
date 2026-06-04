using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

public static class INFileSystem
{
	public static string Root { get; private set; }

	public static bool RootReady { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
	private static AndroidJavaObject m_activity;

	private static AndroidJavaObject m_contentResolver;
	
	private static int m_sdkInt = -1;

	private static AndroidJavaObject m_rootDocument;
#endif

	public static void SetRoot(string root)
	{
		Root = root;
		RootReady = true;
#if UNITY_ANDROID && !UNITY_EDITOR
		if (m_rootDocument != null)
		{
			m_rootDocument.Dispose();
			m_rootDocument = null;
		}
#endif
	}

	public static string Combine(string a, string b)
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		if (UsesSaf())
		{
			a = NormalizePath(a);
			b = NormalizePath(b);
			if (string.IsNullOrEmpty(a))
			{
				return b;
			}
			if (string.IsNullOrEmpty(b))
			{
				return a;
			}
			return a.TrimEnd('/', '\\') + "/" + b.TrimStart('/', '\\');
		}
#endif
		return Path.Combine(a, b);
	}

	public static string GetFileName(string path)
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		if (UsesSaf())
		{
			path = NormalizePath(path);
			int num = path.LastIndexOf('/');
			if (num >= 0)
			{
				return path.Substring(num + 1);
			}
			return path;
		}
#endif
		return Path.GetFileName(path);
	}

	public static string GetDirectoryName(string path)
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		if (UsesSaf())
		{
			path = NormalizePath(path);
			int num = path.LastIndexOf('/');
			if (num >= 0)
			{
				return path.Substring(0, num);
			}
			return string.Empty;
		}
#endif
		return Path.GetDirectoryName(path);
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

	public static void LoadRoot()
	{
		RootReady = false;
#if UNITY_ANDROID && !UNITY_EDITOR
		if (!UsesSaf())
		{
			string text = GetDefaultRoot();
			SetRoot(text);
			Debug.Log("Loaded local Android root: " + text);
			return;
		}
		string text2 = GetPersistedTreeUri();
		if (!string.IsNullOrEmpty(text2))
		{
			SetRoot(text2);
			Debug.Log("Loaded persisted SAF root: " + text2);
			return;
		}
		PickRoot();
		Debug.Log("Waiting for SAF root selection...");
#else
        string text = GetDefaultRoot();
        SetRoot(text);
        Debug.Log("Loaded local filesystem root: " + text);
#endif
	}

	public static string GetExtension(string path)
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		if (UsesSaf())
		{
			path = NormalizePath(path);
			int slash = path.LastIndexOf('/');
			int dot = path.LastIndexOf('.');
			if (dot > slash + 1)
			{
				return path.Substring(dot);
			}
			return string.Empty;
		}
#endif
		return Path.GetExtension(path);
	}

	public static long GetLength(string path)
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		if (UsesSaf())
		{
			InitAndroid();
			AndroidJavaObject androidJavaObject = FindFile(path);
			if (androidJavaObject == null)
			{
				throw new FileNotFoundException(path);
			}
			return androidJavaObject.Call<long>("length");
		}
#endif
		return new System.IO.FileInfo(path).Length;
	}

	public static DateTime GetLastWriteTime(string path)
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		if (UsesSaf())
		{
			InitAndroid();
			AndroidJavaObject androidJavaObject = FindFile(path);
			if (androidJavaObject == null)
			{
				throw new FileNotFoundException(path);
			}
			long num = androidJavaObject.Call<long>("lastModified");
			if (num <= 0L)
			{
				return DateTime.MinValue;
			}
			return DateTimeOffset.FromUnixTimeMilliseconds(num).LocalDateTime;
		}
#endif
		return System.IO.File.GetLastWriteTime(path);
	}

	public static DateTime GetCreationTime(string path)
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		if (UsesSaf())
		{
			throw new NotSupportedException("CreationTime is not available from generic SAF metadata.");
		}
#endif
		return System.IO.File.GetCreationTime(path);
	}

	public static class File
	{
		public static bool Exists(string path)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				return FindFile(path) != null;
			}
#endif
			return System.IO.File.Exists(path);
		}

		public static string ReadAllText(string path)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				return Encoding.UTF8.GetString(ReadAllBytes(path));
			}
#endif
			return System.IO.File.ReadAllText(path);
		}

		public static void WriteAllText(string path, string data)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				WriteAllBytes(path, Encoding.UTF8.GetBytes(data));
				return;
			}
#endif
			string text = GetDirectoryName(path);
			if (!string.IsNullOrEmpty(text) && !System.IO.Directory.Exists(text))
			{
				System.IO.Directory.CreateDirectory(text);
			}
			System.IO.File.WriteAllText(path, data);
		}
		
		public static byte[] ReadAllBytes(string path)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				InitAndroid();
				AndroidJavaObject androidJavaObject = FindFile(path);
				if (androidJavaObject == null)
				{
					throw new FileNotFoundException(path);
				}
				AndroidJavaObject androidJavaObject2 = androidJavaObject.Call<AndroidJavaObject>("getUri");
				if (androidJavaObject2 == null)
				{
					throw new IOException("Failed to resolve URI for file: " + path);
				}
				using (androidJavaObject)
				using (androidJavaObject2)
				{
					AndroidJavaObject androidJavaObject3 = m_contentResolver.Call<AndroidJavaObject>("openInputStream", androidJavaObject2);
					if (androidJavaObject3 == null)
					{
						throw new IOException("Failed to open input stream for file: " + path);
					}
					using (androidJavaObject3)
					using (AndroidJavaObject androidJavaObject4 = new AndroidJavaObject("java.io.ByteArrayOutputStream"))
					{
						while (true)
						{
							int num = androidJavaObject3.Call<int>("read");
							if (num < 0)
							{
								break;
							}
							androidJavaObject4.Call("write", num);
						}
						sbyte[] array = androidJavaObject4.Call<sbyte[]>("toByteArray");
						if (array == null)
						{
							return Array.Empty<byte>();
						}
						byte[] array2 = new byte[array.Length];
						Buffer.BlockCopy(array, 0, array2, 0, array.Length);
						return array2;
					}
				}
			}
#endif
			return System.IO.File.ReadAllBytes(path);
		}

		public static void WriteAllBytes(string path, byte[] data)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				InitAndroid();
				string text1 = NormalizePath(path);
				string text2 = GetFileName(text1);
				AndroidJavaObject androidJavaObject = EnsureDirectory(GetDirectoryName(text1));
				if (androidJavaObject == null)
				{
					throw new IOException("Failed to create or open directory: " + path);
				}
				AndroidJavaObject androidJavaObject2 = FindFile(text1);
				if (androidJavaObject2 == null)
				{
					androidJavaObject2 = androidJavaObject.Call<AndroidJavaObject>("createFile", "application/octet-stream", text2);
				}
				if (androidJavaObject2 == null)
				{
					throw new IOException("Failed to create or open file: " + path);
				}
				AndroidJavaObject androidJavaObject3 = androidJavaObject2.Call<AndroidJavaObject>("getUri");
				if (androidJavaObject3 == null)
				{
					throw new IOException("Failed to resolve URI for file: " + path);
				}
				using (androidJavaObject)
				using (androidJavaObject2)
				using (androidJavaObject3)
				{
					AndroidJavaObject androidJavaObject4 = m_contentResolver.Call<AndroidJavaObject>("openOutputStream", androidJavaObject3, "w");
					if (androidJavaObject4 == null)
					{
						throw new IOException("Failed to open output stream: " + path);
					}
					using (androidJavaObject4)
					using (AndroidJavaObject androidJavaObject5 = new AndroidJavaObject("java.io.BufferedOutputStream", androidJavaObject4, 8192))
					{
						androidJavaObject5.Call("write", data, 0, data.Length);
						androidJavaObject5.Call("flush");
					}
				}
				return;
			}
#endif
			string text = GetDirectoryName(path);
			if (!string.IsNullOrEmpty(text) && !System.IO.Directory.Exists(text))
			{
				System.IO.Directory.CreateDirectory(text);
			}
			System.IO.File.WriteAllBytes(path, data);
		}

		public static FileStream Open(string path, FileMode mode)
		{
			return new FileStream(path, mode);
		}

		public static FileStream Open(string path, FileMode mode, FileAccess access)
		{
			return new FileStream(path, mode, access);
		}

		public static FileStream OpenRead(string path)
		{
			return new FileStream(path, FileMode.Open, FileAccess.Read);
		}

		public static FileStream OpenWrite(string path)
		{
			return new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
		}

		public static void Delete(string path)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				AndroidJavaObject androidJavaObject = FindFile(path);
				if (androidJavaObject == null)
				{
					return;
				}
				if (!androidJavaObject.Call<bool>("delete"))
				{
					throw new IOException("Failed to delete directory: " + path);
				}
				return;
			}
#endif
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);
			}
		}

		public static void Copy(string sourcePath, string destinationPath, bool overwrite)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				if (overwrite && Exists(destinationPath))
				{
					Delete(destinationPath);
				}
				WriteAllBytes(destinationPath, ReadAllBytes(sourcePath));
				return;
			}
#endif
			string text = GetDirectoryName(destinationPath);
			if (!string.IsNullOrEmpty(text) && !System.IO.Directory.Exists(text))
			{
				System.IO.Directory.CreateDirectory(text);
			}
			System.IO.File.Copy(sourcePath, destinationPath, overwrite);
		}

		public static void Move(string sourcePath, string destinationPath)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				WriteAllBytes(destinationPath, ReadAllBytes(sourcePath));
				Delete(sourcePath);
				return;
			}
#endif
			string text = GetDirectoryName(destinationPath);
			if (!string.IsNullOrEmpty(text) && !System.IO.Directory.Exists(text))
			{
				System.IO.Directory.CreateDirectory(text);
			}
			if (System.IO.File.Exists(destinationPath))
			{
				System.IO.File.Delete(destinationPath);
			}
			System.IO.File.Move(sourcePath, destinationPath);
		}

		public static void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				if (!string.IsNullOrEmpty(destinationBackupFileName) && Exists(destinationFileName))
				{
					Copy(destinationFileName, destinationBackupFileName, true);
				}
				Copy(sourceFileName, destinationFileName, true);
				Delete(sourceFileName);
				return;
			}
#endif
			System.IO.File.Replace(sourceFileName, destinationFileName, destinationBackupFileName);
		}

		public static void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				if (!string.IsNullOrEmpty(destinationBackupFileName) && Exists(destinationFileName))
				{
					Copy(destinationFileName, destinationBackupFileName, true);
				}
				Copy(sourceFileName, destinationFileName, true);
				Delete(sourceFileName);
				return;
			}
#endif
			System.IO.File.Replace(sourceFileName, destinationFileName, destinationBackupFileName, ignoreMetadataErrors);
		}
	}

	public static class Directory
	{
		public static bool Exists(string path)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				AndroidJavaObject androidJavaObject = FindDirectory(path);
				if (androidJavaObject == null)
				{
					return false;
				}
				return androidJavaObject.Call<bool>("isDirectory");
			}
#endif
			return System.IO.Directory.Exists(path);
		}

		public static DirectoryInfo CreateDirectory(string path)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				AndroidJavaObject androidJavaObject = EnsureDirectory(path);
				if (androidJavaObject == null)
				{
					throw new IOException("Failed to create directory: " + path);
				}
				return new DirectoryInfo(path);
			}
#endif
			System.IO.Directory.CreateDirectory(path);
			return new DirectoryInfo(path);
		}

		public static void Delete(string path, bool recursive)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				AndroidJavaObject androidJavaObject = FindDirectory(path);
				if (androidJavaObject == null)
				{
					return;
				}
				if (!recursive)
				{
					AndroidJavaObject[] array = androidJavaObject.Call<AndroidJavaObject[]>("listFiles");
					if (array != null && array.Length > 0)
					{
						throw new IOException("Directory is not empty: " + path);
					}
				}
				else
				{
					AndroidJavaObject[] array = androidJavaObject.Call<AndroidJavaObject[]>("listFiles");
					if (array != null)
					{
						for (int i = 0; i < array.Length; i++)
						{
							AndroidJavaObject androidJavaObject2 = array[i];
							if (androidJavaObject2 == null)
							{
								continue;
							}
							if (androidJavaObject2.Call<bool>("isDirectory"))
							{
								Delete(Combine(path, androidJavaObject2.Call<string>("getName")), true);
							}
							else
							{
								File.Delete(Combine(path, androidJavaObject2.Call<string>("getName")));
							}
						}
					}
				}
				if (!androidJavaObject.Call<bool>("delete"))
				{
					throw new IOException("Failed to delete directory: " + path);
				}
				return;
			}
#endif
			if (System.IO.Directory.Exists(path))
			{
				System.IO.Directory.Delete(path, recursive);
			}
		}

		public static string[] GetFiles(string path)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				List<string> list = new List<string>();
				AndroidJavaObject androidJavaObject = FindDirectory(path);
				if (androidJavaObject == null)
				{
					return new string[0];
				}
				AndroidJavaObject[] array = androidJavaObject.Call<AndroidJavaObject[]>("listFiles");
				if (array == null)
				{
					return new string[0];
				}
				for (int i = 0; i < array.Length; i++)
				{
					AndroidJavaObject androidJavaObject2 = array[i];
					if (androidJavaObject2 != null && androidJavaObject2.Call<bool>("isFile"))
					{
						list.Add(Combine(path, androidJavaObject2.Call<string>("getName")));
					}
				}
				return list.ToArray();
			}
#endif
			if (!System.IO.Directory.Exists(path))
			{
				return new string[0];
			}
			return System.IO.Directory.GetFiles(path);
		}

		public static string[] GetDirectories(string path)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				List<string> list = new List<string>();
				AndroidJavaObject androidJavaObject = FindDirectory(path);
				if (androidJavaObject == null)
				{
					return new string[0];
				}
				AndroidJavaObject[] array = androidJavaObject.Call<AndroidJavaObject[]>("listFiles");
				if (array == null)
				{
					return new string[0];
				}
				for (int i = 0; i < array.Length; i++)
				{
					AndroidJavaObject androidJavaObject2 = array[i];
					if (androidJavaObject2 != null && androidJavaObject2.Call<bool>("isDirectory"))
					{
						list.Add(Combine(path, androidJavaObject2.Call<string>("getName")));
					}
				}
				return list.ToArray();
			}
#endif
			if (!System.IO.Directory.Exists(path))
			{
				return new string[0];
			}
			return System.IO.Directory.GetDirectories(path);
		}
	}

	public sealed class FileInfo
	{
		private string m_path;

		public FileInfo(string path)
		{
			m_path = path;
		}

		public string FullName
		{
			get { return m_path; }
		}

		public string Name
		{
			get { return GetFileName(m_path); }
		}

		public string Extension
		{
			get { return GetExtension(m_path); }
		}

		public string DirectoryName
		{
			get { return GetDirectoryName(m_path); }
		}

		public DirectoryInfo Directory
		{
			get
			{
				string text = DirectoryName;
				if (string.IsNullOrEmpty(text))
				{
					return null;
				}

				return new DirectoryInfo(text);
			}
		}

		public bool Exists
		{
			get { return File.Exists(m_path); }
		}

		public long Length
		{
			get { return GetLength(m_path); }
		}

		public DateTime CreationTime
		{
			get { return GetCreationTime(m_path); }
		}

		public DateTime LastWriteTime
		{
			get { return GetLastWriteTime(m_path); }
		}

		public DateTime CreationTimeUtc
		{
			get { return CreationTime.ToUniversalTime(); }
		}

		public DateTime LastWriteTimeUtc
		{
			get { return LastWriteTime.ToUniversalTime(); }
		}

		public void Delete()
		{
			File.Delete(m_path);
		}

		public void CopyTo(string destFileName)
		{
			File.Copy(m_path, destFileName, false);
		}

		public void CopyTo(string destFileName, bool overwrite)
		{
			File.Copy(m_path, destFileName, overwrite);
		}

		public void MoveTo(string destFileName)
		{
			File.Move(m_path, destFileName);
			m_path = destFileName;
		}

		public FileStream OpenRead()
		{
			return File.OpenRead(m_path);
		}

		public FileStream OpenWrite()
		{
			return File.OpenWrite(m_path);
		}

		public StreamReader OpenText()
		{
			return new StreamReader(m_path);
		}

		public StreamWriter CreateText()
		{
			return new StreamWriter(m_path);
		}

		public override string ToString()
		{
			return m_path;
		}
	}

	public sealed class DirectoryInfo
	{
		private string m_path;

		public DirectoryInfo(string path)
		{
			m_path = path;
		}

		public string FullName
		{
			get { return m_path; }
		}

		public string Name
		{
			get { return GetFileName(m_path); }
		}

		public DirectoryInfo Parent
		{
			get
			{
				string text = GetDirectoryName(m_path);
				if (string.IsNullOrEmpty(text))
				{
					return null;
				}

				return new DirectoryInfo(text);
			}
		}

		public bool Exists
		{
			get { return Directory.Exists(m_path); }
		}

		public DateTime CreationTime
		{
			get { return GetCreationTime(m_path); }
		}

		public DateTime LastWriteTime
		{
			get { return GetLastWriteTime(m_path); }
		}

		public DateTime CreationTimeUtc
		{
			get { return CreationTime.ToUniversalTime(); }
		}

		public DateTime LastWriteTimeUtc
		{
			get { return LastWriteTime.ToUniversalTime(); }
		}

		public void Create()
		{
			Directory.CreateDirectory(m_path);
		}

		public DirectoryInfo CreateSubdirectory(string path)
		{
			string text = Combine(m_path, path);
			Directory.CreateDirectory(text);
			return new DirectoryInfo(text);
		}

		public void Delete()
		{
			Directory.Delete(m_path, true);
		}

		public void Delete(bool recursive)
		{
			Directory.Delete(m_path, recursive);
		}

		public FileInfo[] GetFiles()
		{
			string[] array = Directory.GetFiles(m_path);
			FileInfo[] array2 = new FileInfo[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array2[i] = new FileInfo(array[i]);
			}

			return array2;
		}

		public DirectoryInfo[] GetDirectories()
		{
			string[] array = Directory.GetDirectories(m_path);
			DirectoryInfo[] array2 = new DirectoryInfo[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array2[i] = new DirectoryInfo(array[i]);
			}

			return array2;
		}

		public override string ToString()
		{
			return m_path;
		}
	}

	public sealed class FileStream : Stream
	{
		private Stream m_stream;

		private string m_path;

		private FileAccess m_access;

		private FileStream()
		{
		}

		public static implicit operator System.IO.FileStream(FileStream stream)
		{
			if (stream == null)
			{
				return null;
			}

			return stream.m_stream as System.IO.FileStream;
		}

		public static implicit operator FileStream(System.IO.FileStream stream)
		{
			if (stream == null)
			{
				return null;
			}

			FileStream fileStream = new FileStream();
			fileStream.m_stream = stream;
			return fileStream;
		}

		public FileStream(string path, FileMode mode, FileAccess access = FileAccess.ReadWrite)
		{
			m_path = path;
			m_access = access;
#if UNITY_ANDROID && !UNITY_EDITOR
			if (UsesSaf())
			{
				InitAndroid();
				byte[] array = new byte[0];
				bool flag = File.Exists(path);
				if (flag)
				{
					array = File.ReadAllBytes(path);
				}
				if (mode == FileMode.CreateNew)
				{
					if (flag)
					{
						throw new IOException("File already exists.");
					}
					array = new byte[0];
				}
				else if (mode == FileMode.Create)
				{
					array = new byte[0];
				}
				else if (mode == FileMode.Truncate)
				{
					if (!flag)
					{
						throw new FileNotFoundException(path);
					}
					array = new byte[0];
				}
				else if (mode == FileMode.Open)
				{
					if (!flag)
					{
						throw new FileNotFoundException(path);
					}
				}
				else if (mode == FileMode.Append)
				{
					if (!flag)
					{
						array = new byte[0];
					}
					m_access = FileAccess.Write;
				}
				if (m_access == FileAccess.Read)
				{
					m_stream = new MemoryStream(array, false);
				}
				else
				{
					MemoryStream memoryStream = new MemoryStream();
					if (array.Length > 0 && mode != FileMode.Create && mode != FileMode.CreateNew && mode != FileMode.Truncate)
					{
						memoryStream.Write(array, 0, array.Length);
					}
					if (mode == FileMode.Append)
					{
						memoryStream.Position = memoryStream.Length;
					}
					m_stream = memoryStream;
				}
				return;
			}
#endif
			string text = GetDirectoryName(path);
			if (!string.IsNullOrEmpty(text) && !System.IO.Directory.Exists(text))
			{
				System.IO.Directory.CreateDirectory(text);
			}
			FileAccess access2 = access;
			if (mode == FileMode.Append)
			{
				access2 = FileAccess.Write;
			}
			m_stream = new System.IO.FileStream(path, mode, access2, FileShare.None);
		}

		public override bool CanRead
		{
			get { return m_stream.CanRead; }
		}

		public override bool CanSeek
		{
			get { return m_stream.CanSeek; }
		}

		public override bool CanWrite
		{
			get { return m_stream.CanWrite; }
		}

		public override long Length
		{
			get { return m_stream.Length; }
		}

		public override long Position
		{
			get { return m_stream.Position; }
			set { m_stream.Position = value; }
		}

		public override void Flush()
		{
			m_stream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return m_stream.Read(buffer, offset, count);
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return m_stream.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			m_stream.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			m_stream.Write(buffer, offset, count);
		}

		protected override void Dispose(bool disposing)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (disposing && UsesSaf())
			{
				if (m_access != FileAccess.Read)
				{
					MemoryStream memoryStream = m_stream as MemoryStream;
					if (memoryStream != null)
					{
						File.WriteAllBytes(m_path, memoryStream.ToArray());
					}
				}
			}
#endif
			if (disposing && m_stream != null)
			{
				m_stream.Dispose();
			}
			base.Dispose(disposing);
		}
	}

	public sealed class StreamReader : IDisposable
	{
		private System.IO.StreamReader m_reader;

		private StreamReader()
		{
		}

		public static implicit operator System.IO.StreamReader(StreamReader reader)
		{
			if (reader == null)
			{
				return null;
			}

			return reader.m_reader;
		}

		public static implicit operator StreamReader(System.IO.StreamReader reader)
		{
			if (reader == null)
			{
				return null;
			}

			StreamReader streamReader = new StreamReader();
			streamReader.m_reader = reader;
			return streamReader;
		}

		public System.IO.StreamReader GetRealReader()
		{
			return m_reader;
		}

		public StreamReader(string path)
		{
			m_reader = new System.IO.StreamReader(File.OpenRead(path));
		}

		public StreamReader(string path, Encoding encoding)
		{
			m_reader = new System.IO.StreamReader(File.OpenRead(path), encoding);
		}

		public StreamReader(Stream stream)
		{
			m_reader = new System.IO.StreamReader(stream);
		}

		public StreamReader(Stream stream, Encoding encoding)
		{
			m_reader = new System.IO.StreamReader(stream, encoding);
		}

		public bool EndOfStream
		{
			get { return m_reader.EndOfStream; }
		}

		public int Peek()
		{
			return m_reader.Peek();
		}

		public string ReadLine()
		{
			return m_reader.ReadLine();
		}

		public string ReadToEnd()
		{
			return m_reader.ReadToEnd();
		}

		public void Dispose()
		{
			m_reader.Dispose();
		}
	}

	public sealed class BinaryReader : IDisposable
	{
		private System.IO.BinaryReader m_reader;

		private BinaryReader()
		{
		}

		public static implicit operator System.IO.BinaryReader(BinaryReader reader)
		{
			if (reader == null)
			{
				return null;
			}

			return reader.m_reader;
		}

		public static implicit operator BinaryReader(System.IO.BinaryReader reader)
		{
			if (reader == null)
			{
				return null;
			}

			BinaryReader binaryReader = new BinaryReader();
			binaryReader.m_reader = reader;
			return binaryReader;
		}

		public System.IO.BinaryReader GetRealReader()
		{
			return m_reader;
		}

		public BinaryReader(Stream stream)
		{
			m_reader = new System.IO.BinaryReader(stream);
		}

		public BinaryReader(Stream stream, Encoding encoding)
		{
			m_reader = new System.IO.BinaryReader(stream, encoding);
		}

		public Stream BaseStream
		{
			get { return m_reader.BaseStream; }
		}

		public bool ReadBoolean()
		{
			return m_reader.ReadBoolean();
		}

		public int ReadInt32()
		{
			return m_reader.ReadInt32();
		}

		public float ReadSingle()
		{
			return m_reader.ReadSingle();
		}

		public double ReadDouble()
		{
			return m_reader.ReadDouble();
		}

		public string ReadString()
		{
			return m_reader.ReadString();
		}

		public byte[] ReadBytes(int count)
		{
			return m_reader.ReadBytes(count);
		}

		public void Dispose()
		{
			m_reader.Dispose();
		}
	}

	public sealed class StreamWriter : IDisposable
	{
		private System.IO.StreamWriter m_writer;

		private StreamWriter()
		{
		}

		public static implicit operator System.IO.StreamWriter(StreamWriter writer)
		{
			if (writer == null)
			{
				return null;
			}

			return writer.m_writer;
		}

		public static implicit operator StreamWriter(System.IO.StreamWriter writer)
		{
			if (writer == null)
			{
				return null;
			}

			StreamWriter streamWriter = new StreamWriter();
			streamWriter.m_writer = writer;
			return streamWriter;
		}

		public System.IO.StreamWriter GetRealWriter()
		{
			return m_writer;
		}

		public StreamWriter(string path)
		{
			m_writer = new System.IO.StreamWriter(File.OpenWrite(path));
		}

		public StreamWriter(string path, Encoding encoding)
		{
			m_writer = new System.IO.StreamWriter(File.OpenWrite(path), encoding);
		}

		public StreamWriter(Stream stream)
		{
			m_writer = new System.IO.StreamWriter(stream);
		}

		public StreamWriter(Stream stream, Encoding encoding)
		{
			m_writer = new System.IO.StreamWriter(stream, encoding);
		}

		public Stream BaseStream
		{
			get { return m_writer.BaseStream; }
		}

		public Encoding Encoding
		{
			get { return m_writer.Encoding; }
		}

		public bool AutoFlush
		{
			get { return m_writer.AutoFlush; }
			set { m_writer.AutoFlush = value; }
		}

		public void Write(string value)
		{
			m_writer.Write(value);
		}

		public void Write(char value)
		{
			m_writer.Write(value);
		}

		public void Write(char[] buffer)
		{
			m_writer.Write(buffer);
		}

		public void WriteLine()
		{
			m_writer.WriteLine();
		}

		public void WriteLine(string value)
		{
			m_writer.WriteLine(value);
		}

		public void Flush()
		{
			m_writer.Flush();
		}

		public void Dispose()
		{
			m_writer.Dispose();
		}
	}

	public sealed class BinaryWriter : IDisposable
	{
		private System.IO.BinaryWriter m_writer;

		private BinaryWriter()
		{
		}

		public static implicit operator System.IO.BinaryWriter(BinaryWriter writer)
		{
			if (writer == null)
			{
				return null;
			}

			return writer.m_writer;
		}

		public static implicit operator BinaryWriter(System.IO.BinaryWriter writer)
		{
			if (writer == null)
			{
				return null;
			}

			BinaryWriter binaryWriter = new BinaryWriter();
			binaryWriter.m_writer = writer;
			return binaryWriter;
		}

		public System.IO.BinaryWriter GetRealWriter()
		{
			return m_writer;
		}

		public BinaryWriter(Stream stream)
		{
			m_writer = new System.IO.BinaryWriter(stream);
		}

		public BinaryWriter(Stream stream, Encoding encoding)
		{
			m_writer = new System.IO.BinaryWriter(stream, encoding);
		}

		public Stream BaseStream
		{
			get { return m_writer.BaseStream; }
		}

		public void Write(bool value)
		{
			m_writer.Write(value);
		}

		public void Write(byte value)
		{
			m_writer.Write(value);
		}

		public void Write(byte[] buffer)
		{
			m_writer.Write(buffer);
		}

		public void Write(char value)
		{
			m_writer.Write(value);
		}

		public void Write(char[] chars)
		{
			m_writer.Write(chars);
		}

		public void Write(short value)
		{
			m_writer.Write(value);
		}

		public void Write(ushort value)
		{
			m_writer.Write(value);
		}

		public void Write(int value)
		{
			m_writer.Write(value);
		}

		public void Write(uint value)
		{
			m_writer.Write(value);
		}

		public void Write(long value)
		{
			m_writer.Write(value);
		}

		public void Write(ulong value)
		{
			m_writer.Write(value);
		}

		public void Write(float value)
		{
			m_writer.Write(value);
		}

		public void Write(double value)
		{
			m_writer.Write(value);
		}

		public void Write(string value)
		{
			m_writer.Write(value);
		}

		public void Flush()
		{
			m_writer.Flush();
		}

		public void Dispose()
		{
			m_writer.Dispose();
		}
	}

	public static void PollRootLoad()
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		using (AndroidJavaClass androidJavaClass = new AndroidJavaClass(Application.identifier + ".SafPicker"))
		{
			if (!androidJavaClass.CallStatic<bool>("isPickerFinished"))
			{
				return;
			}
			string text = androidJavaClass.CallStatic<string>("consumePickedUri");
			if (!string.IsNullOrEmpty(text))
			{
				SetRoot(text);
				Debug.Log("Picked SAF root: " + text);
			}
			else
			{
				RootReady = true;
				Debug.LogWarning("SAF root selection was cancelled.");
				TerminateAndroidApp();
			}
		}
#endif
	}

#if UNITY_ANDROID && !UNITY_EDITOR
	private static int GetAndroidSdkInt()
	{
		using (AndroidJavaClass androidJavaClass = new AndroidJavaClass("android.os.Build$VERSION"))
		{
			return androidJavaClass.GetStatic<int>("SDK_INT");
		}
	}

	private static bool UsesSaf()
	{
		if (m_sdkInt < 0)
		{
			m_sdkInt = GetAndroidSdkInt();
		}
		return m_sdkInt >= 21;
	}

	private static void TerminateAndroidApp()
	{
		try
		{
			using (AndroidJavaClass androidJavaClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
			{
				AndroidJavaObject androidJavaObject = androidJavaClass.GetStatic<AndroidJavaObject>("currentActivity");
				if (androidJavaObject != null)
				{
					androidJavaObject.Call("finishAndRemoveTask");
					return;
				}
			}
		}
		catch (Exception exception)
		{
			Debug.LogWarning("Failed to terminate Android task: " + exception.Message);
		}
		Application.Quit();
	}

	private static string NormalizePath(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return string.Empty;
		}
		path = path.Replace('\\', '/');
		if (!string.IsNullOrEmpty(Root) && path.StartsWith(Root, StringComparison.Ordinal))
		{
			path = path.Substring(Root.Length).TrimStart('/');
		}
		if (path.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("SAF path does not match the selected root.", nameof(path));
		}
		if (UsesSaf())
		{
			if (Path.IsPathRooted(path))
			{
				throw new ArgumentException("SAF paths must be relative to the selected tree.", nameof(path));
			}
			path = path.TrimStart('/');
			return path;
		}
		if (!Path.IsPathRooted(path) && !string.IsNullOrEmpty(Root))
		{
			path = Path.Combine(Root, path);
		}
		return path;
	}

	private static void InitAndroid()
	{
		if (m_activity != null)
		{
			return;
		}
		using (AndroidJavaClass androidJavaClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
		{
			m_activity = androidJavaClass.GetStatic<AndroidJavaObject>("currentActivity");
		}
		m_contentResolver = m_activity.Call<AndroidJavaObject>("getContentResolver");
	}

	private static string GetPersistedTreeUri()
	{
		InitAndroid();
		AndroidJavaObject androidJavaObject = m_contentResolver.Call<AndroidJavaObject>("getPersistedUriPermissions");
		if (androidJavaObject == null)
		{
			return string.Empty;
		}
		int num = androidJavaObject.Call<int>("size");
		for (int i = 0; i < num; i++)
		{
			AndroidJavaObject androidJavaObject2 = androidJavaObject.Call<AndroidJavaObject>("get", i);
			if (androidJavaObject2 == null)
			{
				continue;
			}
			AndroidJavaObject androidJavaObject3 = androidJavaObject2.Call<AndroidJavaObject>("getUri");
			if (androidJavaObject3 == null)
			{
				continue;
			}
			string text = androidJavaObject3.Call<string>("toString");
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
		}
		return string.Empty;
	}

	private static void PickRoot()
	{
		InitAndroid();
		using (AndroidJavaClass androidJavaClass = new AndroidJavaClass(Application.identifier + ".SafPicker"))
		{
			androidJavaClass.CallStatic("openTreePicker", m_activity);
		}
	}
	
	private static AndroidJavaObject GetRootDocument()
	{
		InitAndroid();
		if (m_rootDocument != null)
		{
			return m_rootDocument;
		}
		using (AndroidJavaClass androidJavaClass = new AndroidJavaClass("android.net.Uri"))
		{
			AndroidJavaObject androidJavaObject = androidJavaClass.CallStatic<AndroidJavaObject>("parse", Root);
			using (AndroidJavaClass androidJavaClass2 = new AndroidJavaClass("androidx.documentfile.provider.DocumentFile"))
			{
				m_rootDocument = androidJavaClass2.CallStatic<AndroidJavaObject>("fromTreeUri", m_activity, androidJavaObject);
				return m_rootDocument;
			}
		}
	}

	private static AndroidJavaObject FindEntry(string path, bool directory)
	{
		InitAndroid();
		string text = NormalizePath(path);
		using (AndroidJavaClass androidJavaClass = new AndroidJavaClass("android.net.Uri"))
		{
			AndroidJavaObject androidJavaObject = androidJavaClass.CallStatic<AndroidJavaObject>("parse", Root);
			using (AndroidJavaClass androidJavaClass2 = new AndroidJavaClass("androidx.documentfile.provider.DocumentFile"))
			{
				AndroidJavaObject androidJavaObject2 = GetRootDocument();
				if (androidJavaObject2 == null)
				{
					return null;
				}
				if (string.IsNullOrEmpty(text))
				{
					if (directory && !androidJavaObject2.Call<bool>("isDirectory"))
					{
						return null;
					}
					if (!directory && !androidJavaObject2.Call<bool>("isFile"))
					{
						return null;
					}
					return androidJavaObject2;
				}
				string[] array = text.Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < array.Length; i++)
				{
					string text2 = array[i];
					AndroidJavaObject androidJavaObject3 = androidJavaObject2.Call<AndroidJavaObject>("findFile", text2);
					if (androidJavaObject3 == null)
					{
						return null;
					}
					androidJavaObject2 = androidJavaObject3;
				}
				if (directory && !androidJavaObject2.Call<bool>("isDirectory"))
				{
					return null;
				}
				if (!directory && !androidJavaObject2.Call<bool>("isFile"))
				{
					return null;
				}
				return androidJavaObject2;
			}
		}
	}

	private static AndroidJavaObject FindFile(string path)
	{
		return FindEntry(path, false);
	}

	private static AndroidJavaObject FindDirectory(string path)
	{
		return FindEntry(path, true);
	}

	private static AndroidJavaObject EnsureDirectory(string path)
	{
		InitAndroid();
		string text = NormalizePath(path);
		using (AndroidJavaClass androidJavaClass = new AndroidJavaClass("android.net.Uri"))
		{
			AndroidJavaObject androidJavaObject = androidJavaClass.CallStatic<AndroidJavaObject>("parse", Root);
			using (AndroidJavaClass androidJavaClass2 = new AndroidJavaClass("androidx.documentfile.provider.DocumentFile"))
			{
				AndroidJavaObject androidJavaObject2 = GetRootDocument();
				if (androidJavaObject2 == null)
				{
					return null;
				}
				if (string.IsNullOrEmpty(text))
				{
					return androidJavaObject2;
				}
				string[] array = text.Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < array.Length; i++)
				{
					string text2 = array[i];
					AndroidJavaObject androidJavaObject3 = androidJavaObject2.Call<AndroidJavaObject>("findFile", text2);
					if (androidJavaObject3 == null)
					{
						androidJavaObject3 = androidJavaObject2.Call<AndroidJavaObject>("createDirectory", text2);
					}
					if (androidJavaObject3 == null)
					{
						return null;
					}
					androidJavaObject2 = androidJavaObject3;
				}
				return androidJavaObject2;
			}
		}
	}
#endif
}