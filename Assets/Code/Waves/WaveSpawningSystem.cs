﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;                 // JobHandle
using Unity.Entities;             // JobComponenetSystem
using Unity.Collections;          // NativeList
using System;                     // IComparable
using Unity.Mathematics;          // math
using Unity.Burst;                // BurstCompile


// cull bottom of the list as it's spawned if performance is an issue
public class WaveSpawningSystem : JobComponentSystem
{

	public struct WaveData : IComparable<WaveData>{
		public float spawnTime; // treated as time to spawn
		public Entity wave;
		public bool spawned;

		public int CompareTo(WaveData other){
			float diff = spawnTime - other.spawnTime;
			return (int)math.sign(diff);
		}
	}

	[BurstCompile]
	public struct WaveSpawnJob : IJobParallelFor{

		public NativeArray<WaveData> waves;

        public EntityCommandBuffer.Concurrent commandBuffer;
        public float time;

		public void Execute(int index){
			if(!waves[index].spawned && waves[index].spawnTime < time){
				commandBuffer.Instantiate(index, waves[index].wave);

				waves[index] = new WaveData{
					spawned = true
				};
			}
		}
	}

	private NativeList<WaveData> waves;
	private float totalTime = 0;
	private EndSimulationEntityCommandBufferSystem commandBufferSystem;

    protected override void OnCreateManager(){
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

	// converts waves from GameObject to Entity version
	public void AddWaves(List<WaveManager.WaveData> goWaves){
		if(!waves.IsCreated){
			waves = new NativeList<WaveData>(Allocator.Persistent);
		}

		// convert into entities and sum spawn times
		float totalTime = 0;
		foreach(WaveManager.WaveData wave in goWaves){
			totalTime += wave.spawnTime;
			waves.Add(new WaveData{
					spawnTime = totalTime,
					// get entity version of gameobject
					wave = GameObjectConversionUtility.ConvertGameObjectHierarchy(
						wave.wavePrefab, World.Active)
				});
		}

		// sort by spawn times
		NativeSortExtension.Sort<WaveData>(waves);
	}

	public void ClearWaves(){
		DisposeContainers();
	}

	public void ResetWaves(){
		totalTime = 0;
	}

    protected override JobHandle OnUpdate(JobHandle dependencies){
    	totalTime += Time.deltaTime;
    	if(waves.IsCreated && waves.Length > 0){
    		dependencies = new WaveSpawnJob{
    				waves = waves,
    				commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
    				time = totalTime
    			}.Schedule(waves.Length, 10, dependencies);

	        // tells buffer systems to wait for the job to finish
	        commandBufferSystem.AddJobHandleForProducer(dependencies);
    	}
    	return dependencies;
    }

    protected override void OnStopRunning()
    {
    	DisposeContainers();
    }

    void DisposeContainers(){
    	if(waves.IsCreated){
        	waves.Dispose();
    	}
    }
}
