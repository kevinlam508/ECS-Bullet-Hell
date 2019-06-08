using System;						// Serializable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using UnityEngine.SceneManagement;  // SceneManager


public class WaveManager : MonoBehaviour
{

	[Serializable]
	public class WaveData{
		[Tooltip("How long to wait after previous wave to spawn this wave")]
		public float spawnTime;
		public GameObject wavePrefab;
	}

	[SerializeField] private List<WaveData> waves = new List<WaveData>();

	void Awake(){

		// init waves for spawn system
		WaveSpawningSystem spawnSystem = World.Active.GetOrCreateSystem<WaveSpawningSystem>();
		spawnSystem.AddWaves(waves);

		// fix spawner exiting
		SceneSwapper.OnSceneExit += spawnSystem.ClearWaves;
		Destroy(gameObject);
	}
}
