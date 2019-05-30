using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;


[UpdateAfter(typeof(BulletMovementSystem))]
[UpdateAfter(typeof(PlayerMovementSystem))]
public class BoundarySystem : JobComponentSystem{


    [BurstCompile]
	struct KeepInBoundJob : IJobForEach<Translation>{

		public float top, bottom, left, right;

		public void Execute(ref Translation trans){
			if(trans.Value.y > top){
				trans.Value.y = top;
			}
			if(trans.Value.y < bottom){
				trans.Value.y = bottom;
			}
			if(trans.Value.x > right){
				trans.Value.x = right;
			}
			if(trans.Value.x < left){
				trans.Value.x = left;
			}
		}
	}

	// doesn't add/remove components, can burst compile commandBuffer
	[BurstCompile]
	struct DeleteOutOfBoundJob : IJobForEachWithEntity<Translation>{

        // stores creates to do after job finishes
        public EntityCommandBuffer.Concurrent commandBuffer;

		public float top, bottom, left, right;

		public void Execute(Entity ent, int idx, [ReadOnly] ref Translation trans){
			if(trans.Value.y > top || trans.Value.y < bottom || trans.Value.x > right 
					|| trans.Value.x < left){
				commandBuffer.DestroyEntity(idx, ent);
			}
		}
	}

    private EntityQuery keepInBound;
    private EntityQuery deleteOnBound;
    private EntityQuery edgeMarkers;

    private float top, bottom, left, right;

    private EndSimulationEntityCommandBufferSystem commandBufferSystem;

    protected override void OnCreateManager(){
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        // will have all entities with Translation and BoundMarkerIn
        keepInBound = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                	typeof(Translation), 
                    ComponentType.ReadOnly<BoundMarkerIn>()
                }
            });

        // will have all entities with Translation and BoundMarkerDelete
        deleteOnBound = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                	ComponentType.ReadOnly<Translation>(), 
                    ComponentType.ReadOnly<BoundMarkerDelete>()
                }
            });

        // will have all entities with Translation and BoundMarkerEdge
        edgeMarkers = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                	ComponentType.ReadOnly<Translation>(), 
                    ComponentType.ReadOnly<BoundMarkerEdge>()
                }
            });

    }

    private void InitBounds(){
		NativeArray<Translation> bounds = edgeMarkers
			.ToComponentDataArray<Translation>(Allocator.TempJob);
		top = right = float.MinValue;
		bottom = left = float.MaxValue;
		for(int i = 0; i < bounds.Length; ++i){
			if(bounds[i].Value.y > top){
				top = bounds[i].Value.y;
			}
			if(bounds[i].Value.y < bottom){
				bottom = bounds[i].Value.y;
			}
			if(bounds[i].Value.x > right){
				right = bounds[i].Value.x;
			}
			if(bounds[i].Value.x < left){
				left = bounds[i].Value.x;
			}
		}
		bounds.Dispose();
    }

	protected override JobHandle OnUpdate(JobHandle handle){
        InitBounds();

		JobHandle inBoundJob = new KeepInBoundJob(){
			top = this.top,
			bottom = this.bottom,
			left = this.left,
			right = this.right
		}.Schedule(keepInBound, handle);

		JobHandle deleteJob = new DeleteOutOfBoundJob(){
			commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
			top = this.top,
			bottom = this.bottom,
			left = this.left,
			right = this.right
		}.Schedule(deleteOnBound, inBoundJob);

        // tell bufferSystem to wait for the process job, then it'll perform
        // buffered commands
        commandBufferSystem.AddJobHandleForProducer(deleteJob);

        return deleteJob;
    }
}
