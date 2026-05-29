using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class INContraptionDataManager
{
	public class ContraptionData
	{
		public struct Unit
		{
			public int Type;

			public int Index;

			public int X;

			public int Y;

			public int Rotation;

			public int Flipped;

			public Unit(int type, int index, int x, int y, int rotation, int flipped)
			{
				Type = type;
				Index = index;
				X = x;
				Y = y;
				Rotation = rotation;
				Flipped = flipped;
			}
		}

		public Unit[] items;

		public ContraptionData()
			: this(0)
		{
		}

		public ContraptionData(int count)
		{
			if (count == 0)
			{
				items = Array.Empty<Unit>();
			}
			else
			{
				items = new Unit[count];
			}
		}

		public static ContraptionData Create(ContraptionDataset contraptionDataset)
		{
			List<ContraptionDataset.ContraptionDatasetUnit> contraptionDatasetList = contraptionDataset.ContraptionDatasetList;
			int count = contraptionDatasetList.Count;
			ContraptionData contraptionData = new ContraptionData(count);
			for (int i = 0; i < count; i++)
			{
				ContraptionDataset.ContraptionDatasetUnit contraptionDatasetUnit = contraptionDatasetList[i];
				contraptionData.items[i] = new Unit((int)((BasePart.PartType)contraptionDatasetUnit.partType).ToSortedPartType(), contraptionDatasetUnit.customPartIndex, contraptionDatasetUnit.x, contraptionDatasetUnit.y, contraptionDatasetUnit.rot, System.Convert.ToInt32(contraptionDatasetUnit.flipped));
			}
			return contraptionData;
		}

		public ContraptionDataset Convert()
		{
			ContraptionDataset contraptionDataset = new ContraptionDataset();
			Unit[] array = items;
			for (int i = 0; i < array.Length; i++)
			{
				Unit unit = array[i];
				contraptionDataset.AddPart(unit.X, unit.Y, (int)((SortedPartType)unit.Type).ToPartType(), unit.Index, (BasePart.GridRotation)unit.Rotation, System.Convert.ToBoolean(unit.Flipped));
			}
			return contraptionDataset;
		}
	}

	private string m_dataDirectory;

	private StringBuilder m_builder;

	public string DataDirectory => m_dataDirectory;

	public static INContraptionDataManager Instance { get; private set; }

	public static ContraptionDataSettings Settings => INUserSettings.Instance.ContraptionDataSettings;

	public static void Create()
	{
		INContraptionDataManager iNContraptionDataManager = new INContraptionDataManager();
		iNContraptionDataManager.Initialize();
		Instance = iNContraptionDataManager;
	}

	public static void SetContraptionData()
	{
		if (INSettings.GetBool(INFeature.NewContraptionData))
		{
			Create();
		}
		else
		{
			Instance = null;
		}
	}

	public void Initialize()
	{
		int versionType = INSettings.VersionType;
		m_builder = new StringBuilder();
		m_dataDirectory = INUnity.DataPath + "/contraptions" + versionType switch
		{
			2 => "A", 
			1 => "O", 
			0 => "", 
			_ => "B", 
		};
		Directory.CreateDirectory(m_dataDirectory);
	}

	public ContraptionDataset LoadContraptionData(string levelName)
	{
		string dataDirectory = m_dataDirectory;
		if (!Settings.Enabled)
		{
			return WPFPrefs.LoadOriginalContraptionDataset(dataDirectory, levelName);
		}
		string text = dataDirectory + "/" + WPFPrefs.ContraptionFileName(levelName);
		string path = dataDirectory + "/" + levelName;
		ContraptionDataset result;
		if (!File.Exists(path))
		{
			result = ((!File.Exists(text)) ? new ContraptionDataset() : WPFPrefs.LoadOriginalContraptionDataset(dataDirectory, levelName));
		}
		else
		{
			TryLoadAndConvert(path, out result);
		}
		if (File.Exists(text))
		{
			if (Settings.BackupOriginalData)
			{
				BackupFile(text, text.Replace(".contraption", ".bak"));
			}
			else
			{
				File.Delete(text);
			}
		}
		return result;
	}

	public void SaveContraptionData(string levelName, ContraptionDataset data)
	{
		string dataDirectory = m_dataDirectory;
		if (!Settings.Enabled)
		{
			WPFPrefs.SaveOriginalContraptionDataset(dataDirectory, levelName, data);
			return;
		}
		string text = dataDirectory + "/" + levelName;
		if (File.Exists(text) && Settings.BackupData)
		{
			BackupFile(text, text + ".bak");
		}
		Save(text, ContraptionData.Create(data));
		if (Settings.SaveAsOriginalData)
		{
			WPFPrefs.SaveOriginalContraptionDataset(dataDirectory, levelName, data);
		}
	}

	private bool TryLoadAndConvert(string path, out ContraptionDataset result)
	{
		try
		{
			result = Load(path).Convert();
			return true;
		}
		catch
		{
			result = new ContraptionDataset();
			return false;
		}
	}

	private bool TryLoad(string path, out ContraptionData result)
	{
		try
		{
			result = LoadCSVFile(path);
			return true;
		}
		catch
		{
			result = new ContraptionData();
			return false;
		}
	}

	public ContraptionData Load(string path)
	{
		switch (Settings.LoadFormat)
		{
		case ContraptionDataSettings.SerializationFormat.ALL:
		{
			if (TryLoadCSVFile(path, out var result))
			{
				return result;
			}
			return LoadJSONFile(path);
		}
		case ContraptionDataSettings.SerializationFormat.CSV:
			return LoadCSVFile(path);
		case ContraptionDataSettings.SerializationFormat.JSON:
			return LoadJSONFile(path);
		default:
			return new ContraptionData();
		}
	}

	private ContraptionData LoadCSVFile(string path)
	{
		using StreamReader streamReader = new StreamReader(path);
		string[] array = streamReader.ReadToEnd().Split(new char[2] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
		int num = array.Length;
		ContraptionData contraptionData = new ContraptionData(num);
		for (int i = 0; i < num; i++)
		{
			string[] array2 = array[i].Split(new char[2] { ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			ContraptionData.Unit unit = new ContraptionData.Unit
			{
				Type = int.Parse(array2[0]),
				Index = int.Parse(array2[1]),
				X = int.Parse(array2[2]),
				Y = int.Parse(array2[3]),
				Rotation = int.Parse(array2[4]),
				Flipped = int.Parse(array2[5])
			};
			contraptionData.items[i] = unit;
		}
		return contraptionData;
	}

	private bool TryLoadCSVFile(string path, out ContraptionData result)
	{
		result = null;
		using StreamReader streamReader = new StreamReader(path);
		string[] array = streamReader.ReadToEnd().Split(new char[2] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
		int num = array.Length;
		ContraptionData contraptionData = new ContraptionData(num);
		for (int i = 0; i < num; i++)
		{
			string[] array2 = array[i].Split(new char[2] { ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			if (array2.Length != 6)
			{
				return false;
			}
			ContraptionData.Unit unit = default(ContraptionData.Unit);
			if (int.TryParse(array2[0], out unit.Type) && int.TryParse(array2[1], out unit.Index) && int.TryParse(array2[2], out unit.X) && int.TryParse(array2[3], out unit.Y) && int.TryParse(array2[4], out unit.Rotation) && int.TryParse(array2[5], out unit.Flipped))
			{
				contraptionData.items[i] = unit;
				continue;
			}
			return false;
		}
		result = contraptionData;
		return true;
	}

	private ContraptionData LoadJSONFile(string path)
	{
		using StreamReader reader = new StreamReader(path);
		return INJsonSerializer.Deserialize<ContraptionData>(reader);
	}

	public void Save(string path, ContraptionData data)
	{
		switch (Settings.SaveFormat)
		{
		case ContraptionDataSettings.SerializationFormat.ALL:
		case ContraptionDataSettings.SerializationFormat.CSV:
			SaveCSVFile(path, data);
			break;
		case ContraptionDataSettings.SerializationFormat.JSON:
			SaveJSONFile(path, data);
			break;
		}
	}

	private void SaveCSVFile(string path, ContraptionData data)
	{
		using StreamWriter streamWriter = new StreamWriter(path);
		StringBuilder builder = m_builder;
		builder.Clear();
		ContraptionData.Unit[] items = data.items;
		for (int i = 0; i < items.Length; i++)
		{
			ContraptionData.Unit unit = items[i];
			string value = ",";
			builder.Append(unit.Type.ToString());
			builder.Append(value);
			builder.Append(unit.Index.ToString());
			builder.Append(value);
			builder.Append(unit.X);
			builder.Append(value);
			builder.Append(unit.Y.ToString());
			builder.Append(value);
			builder.Append(unit.Rotation.ToString());
			builder.Append(value);
			builder.Append(unit.Flipped.ToString());
			builder.AppendLine();
		}
		streamWriter.Write(builder.ToString());
	}

	private void SaveJSONFile(string path, ContraptionData data)
	{
		using StreamWriter writer = new StreamWriter(path);
		INJsonSerializer.Serialize(data, writer);
	}

	private static void BackupFile(string srcPath, string destPath)
	{
		if (!File.Exists(srcPath) || string.Equals(srcPath, destPath, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		try
		{
			if (File.Exists(destPath))
			{
				File.Delete(destPath);
			}
			File.Move(srcPath, destPath);
		}
		catch
		{
		}
	}
}
