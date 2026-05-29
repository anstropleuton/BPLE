public static class CircuitFactory
{
	public static void Connect(ElectricalElement left, ElectricalElement right)
	{
		left.AddConnectedElement(right);
		right.AddConnectedElement(left);
	}

	public static void Disconnect(ElectricalElement left, ElectricalElement right)
	{
		left.RemoveConnectedElement(right);
		right.RemoveConnectedElement(left);
	}

	public static bool IsConnected(ElectricalElement left, ElectricalElement right)
	{
		if (left.Electrodes.Count > right.Electrodes.Count)
		{
			ElectricalElement electricalElement = left;
			left = right;
			right = electricalElement;
		}
		foreach (ElectricalElement.Electrode electrode in left.Electrodes)
		{
			if (electrode.Element == right)
			{
				return true;
			}
		}
		return false;
	}
}
