using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

// contains extra time that needs to be processed by systems because spawning
// does not happen exactly on time
[Serializable]
public struct LostTime: IComponentData{
	public float lostTime;
}

[Serializable]
public struct DelayedReflection: IComponentData{
    public float fraction;
    public float3 reflectNorm;
}

public class TrackerRemovalSystem : JobComponentSystem{
	// stores commands to process after job runs
	private EndSimulationEntityCommandBufferSystem commandBufferSystem;

	// all entities with LostTime
	private EntityQuery lostTimeEntities;

    // all entities with DelayedReflection
    private EntityQuery reflectionEntities;

    protected override void OnCreateManager(){
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        // get entities with LostTime
        lostTimeEntities = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<LostTime>()}
            });

        // get entities with DelayedReflection
        reflectionEntities = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<DelayedReflection>()}
            });
    }

	protected override JobHandle OnUpdate(JobHandle handle){

    	// run job on entities in lostTimeEntities
    	JobHandle lostTimeJob = new RemovalJob<LostTime>{
    		commandBuffer = commandBufferSystem.CreateCommandBuffer()
    	}.ScheduleSingle(lostTimeEntities, handle);
    	
        // run job on entities in lostTimeEntities
        JobHandle reflectJob = new RemovalJob<DelayedReflection>{
            commandBuffer = commandBufferSystem.CreateCommandBuffer()
        }.ScheduleSingle(reflectionEntities, handle);

        JobHandle allJobs = JobHandle.CombineDependencies(lostTimeJob, reflectJob);

        // tells buffer systems to wait for the job to finish, then
        //   it will perform the commands buffered
        commandBufferSystem.AddJobHandleForProducer(allJobs);

        return allJobs;
    }


	struct RemovalJob<T> : IJobForEachWithEntity<T>
        where T : struct, IComponentData{

        public EntityCommandBuffer commandBuffer;

		public void Execute(Entity ent, int index, [ReadOnly] ref T component){
			commandBuffer.RemoveComponent(ent, typeof(T));
		}
	}
}
