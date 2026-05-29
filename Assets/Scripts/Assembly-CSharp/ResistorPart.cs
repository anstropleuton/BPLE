using System.Collections.Generic;

public class ResistorPart : ElectricalPart
{
	private bool m_variable;

	private Resistor m_resistor;

	public int CurrentResistorType => customPartIndex - 6;

	public override IEnumerable<ElectricalElement> ElectricalElements => m_resistor.ToEnumerable();

	public override bool IsTriggerable()
	{
		if (!base.HasGeneratorRef)
		{
			return m_variable;
		}
		return false;
	}

	public override IEnumerable<UIPartTriggerButtonInfo> GetTriggerButtonInfo()
	{
		yield break;
	}

	public override IEnumerable<UIPartSliderButtonInfo> GetSliderButtonInfo()
	{
		if (m_variable)
		{
			yield return new UIPartSliderButtonInfo(UIPartButtonType.Slider, 0, base.Type, 2, base.ConnectedComponent, new UIPartSliderButton.Range((m_resistor == null) ? 1f : m_resistor.Resistance, 1f, 0.1f, 10f, 0.1f, 0.01f));
		}
	}

	public override void OnSliderButtonTriggered(UIPartSliderButton button)
	{
		if (m_variable)
		{
			m_resistor.Resistance = button.Value;
		}
	}

	public override void Initialize()
	{
		base.Initialize();
		m_variable = CurrentResistorType == 5;
	}

	public override void CreateElectricalElements()
	{
		float resistance = 0f;
		switch (CurrentResistorType)
		{
		case 0:
			resistance = 0.01f;
			break;
		case 1:
			resistance = 0.1f;
			break;
		case 2:
			resistance = 1f;
			break;
		case 3:
			resistance = 10f;
			break;
		case 4:
			resistance = 100f;
			break;
		case 5:
			resistance = 1f;
			break;
		}
		m_resistor = new Resistor(resistance);
	}

	protected override BitDirection GetConnectionDirection()
	{
		return BitDirection.LeftAndRight.Rotate((int)m_gridRotation);
	}

	public override void SetRotation(GridRotation rotation)
	{
		int rotation2 = (int)rotation % 2;
		base.SetRotation((GridRotation)rotation2);
	}
}
