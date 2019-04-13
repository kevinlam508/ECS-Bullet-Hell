using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;

// contains extra time that needs to be processed by systems because spawning
// does not happen exactly on time
[Serializable]
public struct LostTime: IComponentData{
	public float lostTime;
}

[UpdateAfter(typeof(BulletMovementSystem))]
public class LostTimeRemoveSystem : JobComponentSystem{
	// stores commands to process after job runs
	private EndSimulationEntityCommandBufferSystem commandBufferSystem;

	// all entities with LostTime
	private EntityQuery entities;

    protected override void OnCreateManager(){
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        // get entities with LostTime
        entities = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<LostTime>()}
            });
    }

	protected override JobHandle OnUpdate(JobHandle handle){

    	// run job on entities in entities
    	JobHandle jobHandle = new LostTimeRemovalJob{
    		commandBuffer = commandBufferSystem.CreateCommandBuffer()
    	}.ScheduleSingle(entities, handle);
    	
        // tells buffer systems to wait for the job to finish, then
        //   it will perform the commands buffered
        commandBufferSystem.AddJobHandleForProducer(jobHandle);

        return jobHandle;
    }


	struct LostTimeRemovalJob : IJobForEachWithEntity<LostTime>{

        public EntityCommandBuffer commandBuffer;

		public void Execute(Entity ent, int index, [ReadOnly] ref LostTime time){
			commandBuffer.RemoveComponent(ent, typeof(LostTime));
		}
	}
}
