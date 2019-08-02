using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;					// ComponentSystem
using Unity.Jobs;						// IJob*
using Unity.Transforms;					// Translation
using Unity.Mathematics;				// math
using Unity.Burst;						// BurstCompile
using Unity.Collections;				// NativeArray

using System.Runtime.CompilerServices;  // MethodImpl


[UpdateAfter(typeof(BulletMovementSystem))]
[UpdateAfter(typeof(PlayerMovementSystem))]
[SystemType(ActiveSystemManager.SystemTypes.Stage)]
public class BoundarySystem : JobComponentSystem{

	public enum InteractionType{
		KeepIn,
		DeleteOutside
	};

	static class BoundUtil{

		enum Directions{ Top, Right, Bottom, Left }

		public static void DefaultBounds(NativeArray<float> bounds){
			bounds[(int)Directions.Top] = bounds[(int)Directions.Right] = float.MinValue;
			bounds[(int)Directions.Bottom] = bounds[(int)Directions.Left] = float.MaxValue;

		}

		public static void InitBounds(NativeArray<Translation> markers, NativeArray<float> bounds){
			for(int i = 0; i < markers.Length; ++i){
				if(markers[i].Value.y > bounds[(int)Directions.Top]){
					bounds[(int)Directions.Top] = markers[i].Value.y;
				}
				if(markers[i].Value.y < bounds[(int)Directions.Bottom]){
					bounds[(int)Directions.Bottom] = markers[i].Value.y;
				}
				if(markers[i].Value.x > bounds[(int)Directions.Right]){
					bounds[(int)Directions.Right] = markers[i].Value.x;
				}
				if(markers[i].Value.x < bounds[(int)Directions.Left]){
					bounds[(int)Directions.Left] = markers[i].Value.x;
				}
			}
		}

		public static void UpdateBounds(Translation pos, NativeArray<float> bounds){
			if(pos.Value.y > bounds[(int)Directions.Top]){
				bounds[(int)Directions.Top] = pos.Value.y;
			}
			if(pos.Value.y < bounds[(int)Directions.Bottom]){
				bounds[(int)Directions.Bottom] = pos.Value.y;
			}
			if(pos.Value.x > bounds[(int)Directions.Right]){
				bounds[(int)Directions.Right] = pos.Value.x;
			}
			if(pos.Value.x < bounds[(int)Directions.Left]){
				bounds[(int)Directions.Left] = pos.Value.x;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Top(NativeArray<float> bounds){
			return bounds[(int)Directions.Top];
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Bottom(NativeArray<float> bounds){
			return bounds[(int)Directions.Bottom];
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Left(NativeArray<float> bounds){
			return bounds[(int)Directions.Left];
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Right(NativeArray<float> bounds){
			return bounds[(int)Directions.Right];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsAboveTop(NativeArray<float> bounds, float val){
			return val > Top(bounds);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsBelowBottom(NativeArray<float> bounds, float val){
			return val < Bottom(bounds);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsBelowLeft(NativeArray<float> bounds, float val){
			return val < Left(bounds);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsAboveRight(NativeArray<float> bounds, float val){
			return val > Right(bounds);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsOutOfBounds(NativeArray<float> bounds, float x, float y){
			return IsAboveRight(bounds, x) || IsBelowLeft(bounds, x) 
				|| IsAboveTop(bounds, y) || IsBelowBottom(bounds, y);
		}
	}

	struct InitBoundsJob : IJobForEachWithEntity<Translation>{

        [NativeDisableParallelForRestriction]
		public NativeArray<float> bounds;

		public void Execute(Entity ent, int idx, [ReadOnly] ref Translation pos){
			if(idx == 0){
				BoundUtil.DefaultBounds(bounds);
			}
			BoundUtil.UpdateBounds(pos, bounds);
		}
	}

	[BurstCompile]
	struct DeleteArrayJob : IJob{
        [DeallocateOnJobCompletion]
        public NativeArray<float> array;

		public void Execute(){}
	}

    [BurstCompile]
	struct KeepInBoundJob : IJobForEach<Translation>{

        [ReadOnly]
		public NativeArray<float> bounds;

		public void Execute(ref Translation trans){
			if(BoundUtil.IsAboveTop(bounds, trans.Value.y)){
				trans.Value.y = BoundUtil.Top(bounds);
			}
			if(BoundUtil.IsBelowBottom(bounds, trans.Value.y)){
				trans.Value.y = BoundUtil.Bottom(bounds);
			}
			if(BoundUtil.IsAboveRight(bounds, trans.Value.x)){
				trans.Value.x = BoundUtil.Right(bounds);
			}
			if(BoundUtil.IsBelowLeft(bounds, trans.Value.x)){
				trans.Value.x = BoundUtil.Left(bounds);
			}
		}
	}

	// doesn't add/remove components, can burst compile commandBuffer
	[BurstCompile]
	struct DeleteOutOfBoundJob : IJobForEachWithEntity<Translation>{

        // stores creates to do after job finishes
        public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly]
		public NativeArray<float> bounds;

		public void Execute(Entity ent, int idx, [ReadOnly] ref Translation trans){
			if(BoundUtil.IsOutOfBounds(bounds, trans.Value.x, trans.Value.y)){
				commandBuffer.DestroyEntity(idx, ent);
			}
		}
	}

	struct ConvertOnEntryJob : IJobForEachWithEntity<Translation, BoundMarkerConvert>{
		public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly]
		public NativeArray<float> bounds;

		public void Execute(Entity ent, int idx, [ReadOnly] ref Translation trans,
				[ReadOnly] ref BoundMarkerConvert convert){
			if(!BoundUtil.IsOutOfBounds(bounds, trans.Value.x, trans.Value.y)){
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

    private BeginInitializationEntityCommandBufferSystem commandBufferSystem;

    protected override void OnCreate(){
        commandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();

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

	protected override JobHandle OnUpdate(JobHandle handle){
        NativeArray<float> bounds = new NativeArray<float>(4, Allocator.TempJob);

        JobHandle initJob = new InitBoundsJob(){
        	bounds = bounds
        }.ScheduleSingle(edgeMarkers, handle);

		JobHandle inBoundJob = new KeepInBoundJob(){
			bounds = bounds
		}.Schedule(keepInBound, initJob);

		// both use the same buffer, so one needs to finish before the other starts
		EntityCommandBuffer.Concurrent buffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent();
		JobHandle deleteJob = new DeleteOutOfBoundJob(){
			commandBuffer = buffer,
			bounds = bounds
		}.Schedule(deleteOnBound, inBoundJob);
		JobHandle convertJob = new ConvertOnEntryJob{
			commandBuffer = buffer,
			bounds = bounds
		}.Schedule(convertInBound, deleteJob);

        // tell bufferSystem to wait for the process job, then it'll perform
        // buffered commands
        commandBufferSystem.AddJobHandleForProducer(convertJob);

        JobHandle deleteArrayJob = new DeleteArrayJob{
        	array = bounds
        }.Schedule(convertJob);
        return deleteArrayJob;
    }
}
