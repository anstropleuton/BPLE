using System.Collections.Generic;

public class PartTrigger : ElectricalPart
{
	private Wire m_wire;

	private LogicLevel m_level;

	private LogicLevel m_newLevel;

	private BasePart m_connectedPart;

	public override IEnumerable<ElectricalElement> ElectricalElements => m_wire.ToEnumerable();

	public override void CreateElectricalElements()
	{
		m_wire = new Wire();
		m_wire.ElementUpdatedEvent += OnElementUpdated;
	}

	protected override BitDirection GetConnectionDirection()
	{
		return BitDirection.Left.Rotate((int)m_gridRotation);
	}

	public override void InitializeElectricalElements()
	{
		m_level = LogicLevel.Invalid;
		int num = 1;
		int num2 = 0;
		for (int i = 0; i < (int)m_gridRotation; i++)
		{
			int num3 = num;
			num = -num2;
			num2 = num3;
		}
		BasePart basePart = base.contraption.FindPartAt(m_coordX + num, m_coordY + num2, this);
		if (basePart != null && basePart.ConnectedComponent == base.ConnectedComponent)
		{
			basePart = ((basePart.m_enclosedPart != null) ? basePart.m_enclosedPart : basePart);
			m_connectedPart = basePart;
		}
	}

	private void OnElementUpdated(CircuitSimulator.SimulationResult result)
	{
		if (result.IsGrounded)
		{
			m_newLevel = ElectricalPart.GetLogicLevel(result.U);
		}
	}

	public override void PreUpdateElements()
	{
		m_newLevel = LogicLevel.Invalid;
	}

	public override void PostUpdateElements()
	{
		SetInvalid(m_newLevel == LogicLevel.Invalid);
		if (m_level != LogicLevel.Invalid && m_newLevel != LogicLevel.Invalid && m_level != m_newLevel && m_connectedPart != null && m_connectedPart.ConnectedComponent == base.ConnectedComponent)
		{
			m_connectedPart.ProcessTouch();
		}
		m_level = m_newLevel;
	}
}
