using System.Collections.Generic;
using UnityEngine;

public class INLocalization
{
	public class LocalizationData
	{
		public List<LocalizationDataElement> Data { get; set; }

		public LocalizationData(List<LocalizationDataElement> data)
		{
			Data = data;
		}
	}

	public class LocalizationDataElement
	{
		public string ID { get; set; }

		public Dictionary<string, string> Text { get; set; }

		public LocalizationDataElement(string id, Dictionary<string, string> text)
		{
			ID = id;
			Text = text;
		}
	}

	private Dictionary<string, string> m_localization;

	private Dictionary<string, string> m_defaultLocalization;

	public static INLocalization Instance { get; private set; }

	public static void Create()
	{
		INLocalization iNLocalization = new INLocalization();
		iNLocalization.Initialize();
		Instance = iNLocalization;
	}

	private void Initialize()
	{
		LocalizationData localizationData = INJsonSerializer.Deserialize<LocalizationData>(INUnity.LoadTextAsset("INLocalizationData").text);
		string key = ((INUnity.Language == SystemLanguage.Chinese) ? "zh-Hans" : "en-US");
		int count = localizationData.Data.Count;
		m_localization = new Dictionary<string, string>(count);
		m_defaultLocalization = new Dictionary<string, string>(count);
		foreach (LocalizationDataElement datum in localizationData.Data)
		{
			string value = datum.Text[key];
			string value2 = datum.Text["en-US"];
			m_localization.Add(datum.ID, value);
			m_defaultLocalization.Add(datum.ID, value2);
		}
	}

	public string GetText(string id)
	{
		return m_localization[id];
	}

	public string GetDefaultText(string id)
	{
		return m_defaultLocalization[id];
	}
}
