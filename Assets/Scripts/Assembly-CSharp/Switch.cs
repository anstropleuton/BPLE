public class Switch : ElectricalElement
{
	private bool m_closed;

	public bool IsClosed => m_closed;

	public Switch()
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
		for (int i = 0; i < base.Electrodes.Count; i++)
		{
			Electrode value = base.Electrodes[i];
			value.SetConnected(closed);
			base.Electrodes[i] = value;
			ElectricalElement element = value.Element;
			int electrodeIndex = element.GetElectrodeIndex(this);
			Electrode value2 = element.Electrodes[electrodeIndex];
			value2.SetConnected(closed);
			element.Electrodes[electrodeIndex] = value2;
		}
	}
}
