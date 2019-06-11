using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

public class TrackerRemovalSystem : JobComponentSystem{
	// stores commands to process after job runs
	private EndSimulationEntityCommandBufferSystem commandBufferSystem;

    protected override void OnCreateManager(){
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

	protected override JobHandle OnUpdate(JobHandle handle){

        // EntityCommandBuffer.Concurrent buffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent();

        // // tells buffer systems to wait for the job to finish, then
        // //   it will perform the commands buffered
        // commandBufferSystem.AddJobHandleForProducer(handle);

        return handle;
    }


	struct RemovalJob<T> : IJobForEachWithEntity<T>
        where T : struct, IComponentData{

        public EntityCommandBuffer.Concurrent commandBuffer;

		public void Execute(Entity ent, int index, [ReadOnly] ref T component){
			commandBuffer.RemoveComponent(index, ent, typeof(T));
		}
	}
}
