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

    //[BurstCompile]
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
            if(points.Length > 0){
        		DynamicBuffer<TimePassed> timeBuffer = timeBuffers[ent];

        		// compute update
        		TimePassed curScale = timeBuffer[pathData.timeIdx];
        		float3 nextPos = EvalConstantSpeed(ref points, ref curScale.time,
        			pathData.speed, pathData.loopIndex);

        		// update components
        		timeBuffer[pathData.timeIdx] = curScale;
        		pos.Value = nextPos;
            }
    	}

    	private float3 EvalConstantSpeed(ref DynamicBuffer<PathPoint> points, 
    			ref float curScale, float speed, int loopIndex){
            bool hasLoop = BezierUtility.HasLoop(loopIndex, points.Length);

    		// approx ideal about to move on scale
    		float travelDist = speed * dt;
			int numSubsteps = 50;
		    int leftIdx = (int)math.floor(curScale);
			for(int i = 0; i < numSubsteps && BezierUtility.IsInBound(curScale, loopIndex, points.Length); ++i){
		    	float localT = curScale - leftIdx;
				curScale += travelDist / numSubsteps / math.length(
					TangentAt(ref points, leftIdx, localT, loopIndex));
				leftIdx = (int)math.floor(curScale);
			}

			// outside curve, return position of last point
			if(!hasLoop && curScale >= points.Length - 1){
				return points[points.Length - 1].position;
			}
            // looping and beyond loop point, reset as if at loop point
            else if(hasLoop && curScale >= points.Length){
                curScale -= points.Length;
                curScale += loopIndex;
                leftIdx = (int)math.floor(curScale);
                return EvalAt(ref points, leftIdx, curScale - leftIdx, loopIndex);
            }
			// still inside curve
			else{
				return EvalAt(ref points, leftIdx, curScale - leftIdx, loopIndex);
			}
    	}

        // returns the point on the curve between startIdx and startIdx + 1
        public Vector3 EvalAt(ref DynamicBuffer<PathPoint> points, int startIdx, 
                float t, int loopIndex){

            if(BezierUtility.HasLoop(loopIndex, points.Length) && startIdx == points.Length - 1){
                return BezierUtility.EvalPoint(points[startIdx].position, 
                    points[startIdx].outTangent, points[loopIndex].inTangent,
                    points[loopIndex].position, t);
            }
            return BezierUtility.EvalPoint(points[startIdx].position, 
                points[startIdx].outTangent, points[startIdx + 1].inTangent,
                points[startIdx + 1].position, t);
        }

        // returns the tangent at the point on the curve between startIdx and startIdx + 1
        public float3 TangentAt(ref DynamicBuffer<PathPoint> points, int startIdx, 
                float t, int loopIndex){

            if(BezierUtility.HasLoop(loopIndex, points.Length) && startIdx == points.Length - 1){
                return BezierUtility.EvalTangent(points[startIdx].position, 
                    points[startIdx].outTangent, points[loopIndex].inTangent,
                    points[loopIndex].position, t);
            }
            return BezierUtility.EvalTangent(points[startIdx].position, 
                points[startIdx].outTangent, points[startIdx + 1].inTangent,
                points[startIdx + 1].position, t);
        }
    }
}
