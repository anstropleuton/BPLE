using System;
using System.Collections.Generic;
using UnityEngine;

public class SingletonSpawner : MonoBehaviour
{
	[Serializable]
	public class PlatformSingleton
	{
		public GameObject singleton;
	}

	[SerializeField]
	private List<PlatformSingleton> m_platformSingletons;

	[SerializeField]
	private List<GameObject> m_commonSingletons;

	private static bool spawnDone;

	public static bool SpawnDone
	{
		get
		{
			return spawnDone;
		}
		set
		{
			spawnDone = value;
		}
	}

	private void Awake()
	{
		Initialize();
	}

	private void Initialize()
	{
		if (spawnDone)
		{
			return;
		}
		// Application.targetFrameRate = 60;
		foreach (GameObject commonSingleton in m_commonSingletons)
		{
			if (!GameObject.Find(commonSingleton.name))
			{
				GameObject obj = UnityEngine.Object.Instantiate(commonSingleton);
				obj.name = commonSingleton.name;
				obj.SetActive(value: true);
			}
		}
		SpawnPlatformSingletons();
		spawnDone = true;
	}

	private void SpawnPlatformSingletons()
	{
		foreach (PlatformSingleton platformSingleton in m_platformSingletons)
		{
			if (!GameObject.Find(platformSingleton.singleton.name))
			{
				GameObject obj = UnityEngine.Object.Instantiate(platformSingleton.singleton);
				obj.name = platformSingleton.singleton.name;
				obj.SetActive(value: true);
			}
		}
	}

	private void OnEnable()
	{
	}
}
