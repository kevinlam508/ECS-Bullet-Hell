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

	public enum InteractionType{
		KeepIn,
		DeleteOutside
	};

	struct BoundUtil{
		public float top, bottom, left, right;

		public bool IsAboveTop(float val){
			return val > top;
		}

		public bool IsBelowBottom(float val){
			return val < bottom;
		}

		public bool IsBelowLeft(float val){
			return val < left;
		}

		public bool IsAboveRight(float val){
			return val > right;
		}

		public bool IsOutOfBounds(float x, float y){
			return IsAboveRight(x) || IsBelowLeft(x) || IsAboveTop(y)
				|| IsBelowBottom(y);
		}
	}

    [BurstCompile]
	struct KeepInBoundJob : IJobForEach<Translation>{

		public BoundUtil util;

		public void Execute(ref Translation trans){
			if(util.IsAboveTop(trans.Value.y)){
				trans.Value.y = util.top;
			}
			if(util.IsBelowBottom(trans.Value.y)){
				trans.Value.y = util.bottom;
			}
			if(util.IsAboveRight(trans.Value.x)){
				trans.Value.x = util.right;
			}
			if(util.IsBelowLeft(trans.Value.x)){
				trans.Value.x = util.left;
			}
		}
	}

	// doesn't add/remove components, can burst compile commandBuffer
	[BurstCompile]
	struct DeleteOutOfBoundJob : IJobForEachWithEntity<Translation>{

        // stores creates to do after job finishes
        public EntityCommandBuffer.Concurrent commandBuffer;
		public BoundUtil util;

		public void Execute(Entity ent, int idx, [ReadOnly] ref Translation trans){
			if(util.IsOutOfBounds(trans.Value.x, trans.Value.y)){
				commandBuffer.DestroyEntity(idx, ent);
			}
		}
	}

	struct ConvertOnEntryJob : IJobForEachWithEntity<Translation, BoundMarkerConvert>{
		public EntityCommandBuffer.Concurrent commandBuffer;
		public BoundUtil util;

		public void Execute(Entity ent, int idx, [ReadOnly] ref Translation trans,
				[ReadOnly] ref BoundMarkerConvert convert){
			if(!util.IsOutOfBounds(trans.Value.x, trans.Value.y)){
				commandBuffer.RemoveComponent(idx, ent, typeof(BoundMarkerConvert));

				// add new marker
				switch(convert.newMarker){
					case InteractionType.KeepIn:
						commandBuffer.AddComponent(idx, ent, new BoundMarkerIn());
						break;
					case InteractionType.DeleteOutside:
						commandBuffer.AddComponent(idx, ent, new BoundMarkerDelete());
						break;
				}
			}
		}
	}

    private EntityQuery keepInBound;
    private EntityQuery deleteOnBound;
    private EntityQuery convertInBound;
    private EntityQuery edgeMarkers;

    private BoundUtil util;

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

        // will have all entities with Translation and BoundMarkerConvert
        convertInBound = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                	ComponentType.ReadOnly<Translation>(), 
                    ComponentType.ReadOnly<BoundMarkerConvert>()
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
		float top, bottom, right, left;
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

		util = new BoundUtil{
				top = top,
				bottom = bottom,
				left = left,
				right = right
			};
    }

	protected override JobHandle OnUpdate(JobHandle handle){
        InitBounds();

		JobHandle inBoundJob = new KeepInBoundJob(){
			util = util
		}.Schedule(keepInBound, handle);

		// both use the same buffer, so one needs to finish before the other starts
		EntityCommandBuffer.Concurrent buffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent();
		JobHandle deleteJob = new DeleteOutOfBoundJob(){
			commandBuffer = buffer,
			util = util
		}.Schedule(deleteOnBound, inBoundJob);
		JobHandle convertJob = new ConvertOnEntryJob{
			commandBuffer = buffer,
			util = util
		}.Schedule(convertInBound, deleteJob);

        // tell bufferSystem to wait for the process job, then it'll perform
        // buffered commands
        commandBufferSystem.AddJobHandleForProducer(convertJob);
        return convertJob;
    }
}
