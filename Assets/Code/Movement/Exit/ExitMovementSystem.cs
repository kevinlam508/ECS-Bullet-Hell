using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;     // JobComponentSystem
using Unity.Jobs;         // IJob*
using Unity.Transforms;   // Traslation, Rotation
using Unity.Burst;        // BurstCompile
using Unity.Collections;  // ReadOnly

public class ExitMovementSystem : JobComponentSystem{

	public enum MovementComponent{ None, Simple, Path }

	// other systems
    EndSimulationEntityCommandBufferSystem commandBufferSystem;

    // entities to work on
	EntityQuery movers;

	protected override void OnCreateManager(){
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
		movers = GetEntityQuery(
			ComponentType.ReadOnly<ExitMovement>(),
			typeof(TimePassed));
	}

    protected override JobHandle OnUpdate(JobHandle handle){

    	JobHandle moveJob = new ExitMovementJob{
    			dt = Time.deltaTime,
    			timeBuffers = GetBufferFromEntity<TimePassed>(),
    			buffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent()
    		}.Schedule(movers, handle);

        commandBufferSystem.AddJobHandleForProducer(moveJob);
    	return moveJob;
    }

    struct ExitMovementJob : IJobForEachWithEntity<ExitMovement>{
    	public float dt;

    	// used across multiple jobs, but made it so each system only uses
    	//   1 element from the buffer per entity
        [NativeDisableParallelForRestriction]
    	public BufferFromEntity<TimePassed> timeBuffers;

    	public EntityCommandBuffer.Concurrent buffer;

    	public void Execute(Entity ent, int idx, [ReadOnly] ref ExitMovement exitMove){
    		// update time
    		DynamicBuffer<TimePassed> timeBuffer = timeBuffers[ent];
    		TimePassed time = timeBuffer[exitMove.timeIdx];
    		time.time += dt;

    		// time to exit
    		if(time.time > exitMove.exitTime){
    			// remove old component
    			switch(exitMove.originalMovement){
    				case MovementComponent.Simple:
    					buffer.RemoveComponent<BulletMovement>(idx, ent);
    					break;
    				case MovementComponent.Path:
    					buffer.RemoveComponent<PathMovement>(idx, ent);
    					buffer.RemoveComponent<PathPoint>(idx, ent);
    					break;
    			}

    			// add new component
    			buffer.AddComponent(idx, ent, new BulletMovement{
    					moveType = BulletMovementSystem.MoveType.LINEAR,
    					moveSpeed = exitMove.exitSpeed,
    					rotateSpeed = 0f
    				});

    			// remove this component
    			buffer.RemoveComponent<ExitMovement>(idx, ent);
    		}
    		// not time to exit, write the new time to the buffer
    		else{
    			timeBuffer[exitMove.timeIdx] = time;
    		}
    	}
    }
}
