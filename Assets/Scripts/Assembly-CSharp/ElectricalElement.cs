using System;
using System.Collections.Generic;

public abstract class ElectricalElement
{
	public struct Electrode
	{
		public ElectricalElement Element;

		public int Index;

		public int Type;

		public bool IsConnected;

		private int m_connectionStack;

		private static Electrode s_empty = new Electrode(null, -1, -1);

		public static Electrode Empty => s_empty;

		public bool IsEmpty => Index == -1;

		public Electrode(ElectricalElement element, int index, int type)
			: this(element, index, type, isConnected: true)
		{
		}

		public Electrode(ElectricalElement element, int index, int type, bool isConnected)
		{
			Element = element;
			Index = index;
			Type = type;
			IsConnected = isConnected;
			m_connectionStack = 0;
		}

		public void SetConnected(bool connected)
		{
			m_connectionStack += ((!connected) ? 1 : (-1));
			if (m_connectionStack < 0)
			{
				m_connectionStack = 0;
			}
			IsConnected = m_connectionStack == 0;
		}
	}

	public const int DefaultElectrodeType = -1;

	private List<Electrode> m_electrodes;

	public int ElementIndex { get; set; }

	public int CircuitIndex { get; set; }

	public List<Electrode> Electrodes => m_electrodes;

	public IEnumerable<Electrode> ConnectedElectrodes
	{
		get
		{
			foreach (Electrode electrode in m_electrodes)
			{
				if (electrode.IsConnected)
				{
					yield return electrode;
				}
			}
		}
	}

	public event Action<CircuitSimulator.SimulationResult> ElementUpdatedEvent;

	public ElectricalElement()
	{
		m_electrodes = new List<Electrode>();
	}

	public virtual void Initialize()
	{
	}

	public virtual void Update()
	{
	}

	public int GetConnectedElectrodeCount()
	{
		int num = 0;
		foreach (Electrode electrode in m_electrodes)
		{
			if (electrode.IsConnected)
			{
				num++;
			}
		}
		return num;
	}

	public Electrode GetElectrodeByIndex(int index)
	{
		return m_electrodes[index];
	}

	public Electrode GetConnectedElectrodeByIndex(int index)
	{
		Electrode result = m_electrodes[index];
		if (!result.IsConnected)
		{
			return Electrode.Empty;
		}
		return result;
	}

	public Electrode GetElectrodeByType(int type)
	{
		foreach (Electrode electrode in m_electrodes)
		{
			if (electrode.Type == type)
			{
				return electrode;
			}
		}
		return Electrode.Empty;
	}

	public Electrode GetConnectedElectrodeByType(int type)
	{
		foreach (Electrode electrode in m_electrodes)
		{
			if (electrode.Type == type && electrode.IsConnected)
			{
				return electrode;
			}
		}
		return Electrode.Empty;
	}

	public Electrode GetAnotherElectrode(Electrode electrode)
	{
		return GetAnotherElectrode(electrode.Element);
	}

	public Electrode GetAnotherElectrode(ElectricalElement element)
	{
		for (int i = 0; i < m_electrodes.Count; i++)
		{
			Electrode result = m_electrodes[i];
			if (result.Element != element)
			{
				return result;
			}
		}
		return Electrode.Empty;
	}

	public Electrode GetAnotherConnectedElectrode(ElectricalElement element)
	{
		for (int i = 0; i < m_electrodes.Count; i++)
		{
			Electrode result = m_electrodes[i];
			if (result.Element != element && result.IsConnected)
			{
				return result;
			}
		}
		return Electrode.Empty;
	}

	public int GetAnotherElectrode(int electrode)
	{
		for (int i = 0; i < m_electrodes.Count; i++)
		{
			if (i != electrode)
			{
				return i;
			}
		}
		return -1;
	}

	public int GetAnotherConnectedElectrode(int electrode)
	{
		for (int i = 0; i < m_electrodes.Count; i++)
		{
			if (i != electrode && m_electrodes[i].IsConnected)
			{
				return i;
			}
		}
		return -1;
	}

	public int GetElectrodeIndex(Electrode electrode)
	{
		return GetElectrodeIndex(electrode.Element);
	}

	public int GetElectrodeIndex(ElectricalElement element)
	{
		for (int i = 0; i < m_electrodes.Count; i++)
		{
			if (m_electrodes[i].Element == element)
			{
				return i;
			}
		}
		return -1;
	}

	public virtual bool IsNode()
	{
		return false;
	}

	public virtual void UpdateElectrode(CircuitSimulator.SimulationResult result)
	{
		this.ElementUpdatedEvent?.Invoke(result);
	}

	public void AddConnectedElement(ElectricalElement element)
	{
		AddConnectedElement(element, -1);
	}

	public void AddConnectedElement(ElectricalElement element, int type)
	{
		Electrode item = new Electrode(element, m_electrodes.Count, type);
		m_electrodes.Add(item);
	}

	public void RemoveConnectedElement(ElectricalElement element)
	{
		int num = -1;
		for (int i = 0; i < m_electrodes.Count; i++)
		{
			if (m_electrodes[i].Element == element)
			{
				num = i;
				break;
			}
		}
		if (num != -1)
		{
			m_electrodes.RemoveAt(num);
		}
	}
}
