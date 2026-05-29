using System;
using UnityEngine;

public class PhysicsSettings : SettingsBase
{
	public float GravityX { get; set; }

	public float GravityY { get; set; }

	public bool NoDrag { get; set; }

	public float TimeScale { get; set; }

	public PhysicsSettings()
	{
		GravityX = Physics.gravity.x;
		GravityY = Physics.gravity.y;
		NoDrag = false;
		TimeScale = 1f;
	}

	public PhysicsSettings(PhysicsSettings settings)
	{
		GravityX = settings.GravityX;
		GravityY = settings.GravityY;
		NoDrag = settings.NoDrag;
		TimeScale = settings.TimeScale;
	}

	public override void Initialize()
	{
		Physics.gravity = new Vector2(GravityX, GravityY);
		INSettings.SetValue(INFeature.NoDrag, new Variant(new ValueVariant(NoDrag)));
		INSettings.SetValue(INFeature.TimeScale, new Variant(new ValueVariant(TimeScale)));
	}

	public void SetGravityX(float value)
	{
		GravityX = value;
		Physics.gravity = new Vector2(GravityX, GravityY);
	}

	public void SetGravityY(float value)
	{
		GravityY = value;
		Physics.gravity = new Vector2(GravityX, GravityY);
	}

	public void SetNoDrag(bool value)
	{
		NoDrag = value;
		INSettings.SetValue(INFeature.NoDrag, new Variant(new ValueVariant(NoDrag)));
	}

	public void SetTimeScale(float value)
	{
		TimeScale = Math.Clamp(value, 0f, 10f);
		INSettings.SetValue(INFeature.TimeScale, new Variant(new ValueVariant(TimeScale)));
	}
}
