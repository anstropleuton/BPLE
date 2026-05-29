using System.Collections.Generic;
using UnityEngine;

public class ColoredFrameManager : PartManager
{
	protected override void Initialize()
	{
		base.Initialize();
		m_status = (StatusCode)3;
	}

	public override void FixedUpdate()
	{
		Contraption instance = Contraption.Instance;
		List<ColoredFrame> list = new List<ColoredFrame>();
		List<ColoredFrame> list2 = new List<ColoredFrame>();
		foreach (BasePart part in instance.Parts)
		{
			if (part.IsColoredrame())
			{
				if (part.IsTransparentFrame())
				{
					list2.Add(part as ColoredFrame);
				}
				list.Add(part as ColoredFrame);
			}
		}
		if (!INSettings.GetBool(INFeature.CanColorTransparentFrame))
		{
			return;
		}
		float t = INSettings.GetFloat(INFeature.TransparentFrameColorDecayRate);
		float num = INSettings.GetFloat(INFeature.TransparentFrameAlphaDecayRate);
		for (int i = 0; i < 2; i++)
		{
			foreach (ColoredFrame item in list2)
			{
				int num2 = 1;
				int num3 = 0;
				Color a = item.Color * item.Color.a;
				float num4 = item.Color.a;
				for (int j = 0; j < 4; j++)
				{
					BasePart basePart = instance.FindPartAt(item.m_coordX + num2, item.m_coordY + num3, item);
					if (basePart != null && basePart is ColoredFrame coloredFrame)
					{
						float a2 = coloredFrame.Color.a;
						a += coloredFrame.Color * a2;
						num4 += a2;
					}
					int num5 = num2;
					num2 = -num3;
					num3 = num5;
				}
				a /= num4;
				a = Color.Lerp(a, item.TransparentColor, t);
				a.a = a.a * (1f - num) + item.TransparentColor.a * num;
				item.Color = a;
			}
		}
		foreach (ColoredFrame item2 in list2)
		{
			item2.UpdateRenderers();
		}
	}
}
