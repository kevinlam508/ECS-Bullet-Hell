using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;     // JobComponentSystem
using Unity.Jobs;         // IJob*
using Unity.Transforms;   // Traslation, Rotation
using Unity.Mathematics;  // math
using Unity.Burst;        // BurstCompile
using Unity.Collections;  // ReadOnly

public class PathMovementSystem : JobComponentSystem{

    private EntityQuery pathMovers;

	protected override void OnCreateManager(){

		pathMovers = GetEntityQuery(
			typeof(Translation), 
			ComponentType.ReadOnly<PathPoint>(), 
			ComponentType.ReadOnly<PathMovement>(),
			typeof(TimePassed));
	}

    protected override JobHandle OnUpdate(JobHandle handle){

    	JobHandle pathJob = new PathMovementJob{
    			dt = Time.deltaTime,
    			pointBuffers = GetBufferFromEntity<PathPoint>(true),
    			timeBuffers = GetBufferFromEntity<TimePassed>(false)
    		}.Schedule(pathMovers, handle);

    	return pathJob;
    }

    [BurstCompile]
    struct PathMovementJob : IJobForEachWithEntity<PathMovement, Translation>{

    	public float dt;
    	[ReadOnly] public BufferFromEntity<PathPoint> pointBuffers;

    	// used across multiple jobs, but made it so each system only uses
    	//   1 element from the buffer per entity
        [NativeDisableParallelForRestriction]
    	public BufferFromEntity<TimePassed> timeBuffers;

    	public void Execute(Entity ent, int idx, [ReadOnly] ref PathMovement pathData,
    			ref Translation pos){
    		DynamicBuffer<PathPoint> points = pointBuffers[ent];
    		DynamicBuffer<TimePassed> timeBuffer = timeBuffers[ent];

    		// compute update
    		TimePassed curScale = timeBuffer[pathData.timeIdx];
    		float3 nextPos = EvalConstantSpeed(ref points, ref curScale.time,
    			pathData.speed);

    		// update components
    		timeBuffer[pathData.timeIdx] = curScale;
    		pos.Value = nextPos;
    	}

    	private float3 EvalConstantSpeed(ref DynamicBuffer<PathPoint> points, 
    			ref float curScale, float speed){

    		// approx ideal about to move on scale
    		float travelDist = speed * dt;
			int numSubsteps = 10;
		    int leftIdx = (int)math.floor(curScale);
			for(int i = 0; i < numSubsteps && curScale < points.Length - 1; ++i){
		    	float localT = curScale - leftIdx;
				curScale += travelDist / numSubsteps / math.length(
					BezierUtility.EvalTangent(points[leftIdx].position, 
						points[leftIdx].outTangent, points[leftIdx + 1].inTangent, 
						points[leftIdx + 1].position, localT));
				leftIdx = (int)math.floor(curScale);
			}

			// outside curve, return position of last point
			if(curScale >= points.Length - 1){
				return points[points.Length - 1].position;
			}
			// still inside curve
			else{
				return BezierUtility.EvalPoint(points[leftIdx].position, 
					points[leftIdx].outTangent, points[leftIdx + 1].inTangent, 
					points[leftIdx + 1].position, curScale - leftIdx);
			}
    	}
    }
}
