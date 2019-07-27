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
	private WaveSpawningSystem spawnSystem;

	void Awake(){

		// init waves for spawn system
		spawnSystem = World.Active.GetOrCreateSystem<WaveSpawningSystem>();
		if(waves.Count > 0){
			spawnSystem.AddWaves(waves);
			spawnSystem.ResetTime();
			spawnSystem.Enabled = true;

			// fix spawner exitings
			SceneSwapper.OnSceneExit += SceneExit;
		}
	}

	private void SceneExit(){
		spawnSystem.Enabled = false;
		spawnSystem.ClearWaves();

		SceneSwapper.OnSceneExit -= SceneExit;
	}
}
