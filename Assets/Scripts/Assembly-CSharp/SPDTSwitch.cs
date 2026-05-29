public class SPDTSwitch : ElectricalElement
{
	private bool m_closed;

	public const int PoleType = 0;

	public const int Throw1Type = 1;

	public const int Throw2Type = 2;

	public bool IsClosed => m_closed;

	public Electrode Pole => GetElectrodeByType(0);

	public Electrode Throw1 => GetElectrodeByType(1);

	public Electrode Throw2 => GetElectrodeByType(2);

	public SPDTSwitch()
	{
		m_closed = false;
	}

	public override void Initialize()
	{
		SetClosedInternal(m_closed);
	}

	public void SetClosed(bool closed)
	{
		if (m_closed != closed)
		{
			m_closed = closed;
			SetClosedInternal(closed);
		}
	}

	private void SetClosedInternal(bool closed)
	{
		for (int i = 0; i < 2; i++)
		{
			Electrode value = ((i == 0) ? Throw1 : Throw2);
			bool connected = ((i == 0) ? (!closed) : closed);
			if (!value.IsEmpty)
			{
				value.SetConnected(connected);
				base.Electrodes[value.Index] = value;
				ElectricalElement element = value.Element;
				int electrodeIndex = element.GetElectrodeIndex(this);
				Electrode value2 = element.Electrodes[electrodeIndex];
				value2.SetConnected(connected);
				element.Electrodes[electrodeIndex] = value2;
			}
		}
	}
}
