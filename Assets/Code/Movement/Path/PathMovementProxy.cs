using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// dots
using Unity.Mathematics;
using Unity.Entities;               // IComponentData, IConvertGameObjectToEntity

[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class PathMovementProxy : MonoBehaviour, IConvertGameObjectToEntity
{
	[Serializable]
	public class Point{
		public Vector3 position;
		public Vector3 inTangent;
		public Vector3 outTangent;
		public bool makeSmoothTangent;
		[HideInInspector] public int controlId;		// hash for scene GUI elements
		public bool isSelected;

		public Point(){}

		public Point(Point p){
			position = p.position;
			inTangent = p.inTangent;
			outTangent = p.outTangent;
			makeSmoothTangent = p.makeSmoothTangent;
		}
	}

	[Tooltip("Speed that the game object will move in the preview")]
	public float speed = 1f;
	public List<Point> points = new List<Point>();

	public int NumPoints => points.Count;
	public Point this[int i] => points[i];
	public bool HasSelected{
		get{
			foreach(Point p in points){
				if(p.isSelected){
					return true;
				}
			}
			return false;
		}
	}

	public void ResetSelection(){
		foreach(Point p in points){
			p.isSelected = false;
		}
	}

	public void SelectAll(){
		foreach(Point p in points){
			p.isSelected = true;
		}
	}

	public void DeletePoint(int idx){
		if(idx > NumPoints - 1){
			Debug.LogError(idx + " is out of bounds");
		}

		// at the ends, can just remove
		if(idx == 0 || idx == NumPoints - 1){
			points.RemoveAt(idx);
		}
		// in the middle, double the tangents of the adjacent points
		else{
			Vector3 temp = points[idx - 1].outTangent - points[idx - 1].position;
			points[idx - 1].outTangent += temp;
			temp = points[idx + 1].inTangent - points[idx + 1].position;
			points[idx + 1].inTangent += temp;

			points.RemoveAt(idx);
		}
	}

	public void InsertPoint(int idx){
		InsertPoint(idx, null);
	}

	public void InsertPoint(int idx, Point p){
		if(idx > NumPoints){
			Debug.LogError(idx + " is out of bounds");
		}

		// make a new point if one isn't provided
		Point newPoint = (p == null) ? new Point() : new Point(p);
		if(p == null){
			if(NumPoints == 0){
				newPoint.position = Vector3.zero;
				newPoint.inTangent = Vector3.left;
				newPoint.outTangent = Vector3.right;
			}
			// add to front
			else if(idx == 0){
				newPoint.position = points[0].position + Vector3.up;
				newPoint.inTangent = newPoint.position + Vector3.left;
				newPoint.outTangent = newPoint.position + Vector3.right;
			}
			// add to back
			else if(idx == NumPoints){
				newPoint.position = points[NumPoints - 1].position + Vector3.up;
				newPoint.inTangent = newPoint.position + Vector3.left;
				newPoint.outTangent = newPoint.position + Vector3.right;
			}
			// add to middle
			else{
				newPoint.position = EvalAt(idx - 1, .5f);
				Vector3[] oldControl = new Vector3[4];

				// update tangents
				oldControl[0] = points[idx - 1].position;
				oldControl[1] = points[idx - 1].outTangent;
				oldControl[2] = points[idx].inTangent;
				oldControl[3] = points[idx].position;
				GetNewTangents(oldControl, ref points[idx - 1].outTangent, 
					ref newPoint.inTangent, ref newPoint.outTangent, 
					ref points[idx].inTangent);

				newPoint.makeSmoothTangent = true;
			}
		}
		points.Insert(idx, newPoint);
	}

	// Provide where to start the search and the minimum size as args
	// Will be replaced with the start of the segment and its length or 
	//   -1 if none found
	private void FindLongestSegment(ref int startIdx, ref int length){
		int longestSegment = length;
        int segementStartIdx = -1;
        int curIdx;
        for(int i = startIdx; i < NumPoints; ++i){
            if(points[i].isSelected){
                curIdx = i;
                while(i < NumPoints && points[i].isSelected){
                    ++i;
                }

                if(i - curIdx > longestSegment){
                    segementStartIdx = curIdx;
                    longestSegment = i - curIdx;
                }
            }
        }

        startIdx = segementStartIdx;
        if(segementStartIdx != -1){
        	length = longestSegment;
        }
        else{
        	length = -1;
        }
	}

	public void DuplicateLongestSelectedSegment(){
        // find the longest selected segment
        int longestSegment = 0;
        int segementStartIdx = 0;
        FindLongestSegment(ref segementStartIdx, ref longestSegment);

        // duplicate its points
        for(int i = 0; i < longestSegment; ++i){
            InsertPoint(NumPoints, points[i + segementStartIdx]);
        }

        // select only the new points
        ResetSelection();
        for(int i = 0; i < longestSegment; ++i){
            points[NumPoints - 1 - i].isSelected = true;
        }
	}

	public void MakeCircleFromLongestSegment(){
        // find the longest selected segment
        int longestSegment = 2;
        int segementStartIdx = 0;
        FindLongestSegment(ref segementStartIdx, ref longestSegment);

        switch(longestSegment){
        	case -1:	// none found, do nothing
        		break;
        	case 2:
        		Make2PtCircle(segementStartIdx);
        		break;
        	case 3:
        		break;
        	default:
        		break;
        }

	}

	private void Make2PtCircle(int idx){
		
	}

	public static void GetNewTangents(Vector3[] oldControl, ref Vector3 leftOut, 
			ref Vector3 midIn, ref Vector3 midOut, ref Vector3 rightIn){
		if(oldControl.Length != 4){
			Debug.LogError("Not enough points to get control points");
		}

		leftOut = (oldControl[0] + oldControl[1]) / 2;
		rightIn = (oldControl[2] + oldControl[3]) / 2;
		Vector3 midMidpoint = (oldControl[2] + oldControl[1]) / 2;
		midIn = (leftOut + midMidpoint) / 2;
		midOut = (rightIn + midMidpoint) / 2;
	}

	// returns the point on the curve between startIdx and startIdx + 1
	public Vector3 EvalAt(int startIdx, float t){
		if(startIdx >= NumPoints - 1){
			Debug.LogError("Invalid startIdx: " + startIdx + " in EvalAt");
		}

		return BezierUtility.EvalPoint(points[startIdx].position, 
			points[startIdx].outTangent, points[startIdx + 1].inTangent,
			points[startIdx + 1].position, t);
	}

	public Vector3 TangentAt(int startIdx, float t){
		if(startIdx >= NumPoints - 1){
			Debug.LogError("Invalid startIdx: " + startIdx + " in TangentAt");
		}

    	return BezierUtility.EvalTangent(points[startIdx].position, 
			points[startIdx].outTangent, points[startIdx + 1].inTangent,
			points[startIdx + 1].position, t);
	}

	// curScale will contain the next scale to use 
	public Vector3 EvalConstantSpeed(ref float curScale, float dt){
		// approx ideal dt for desired speed in steps
		float travelDist = speed * dt;
		int numSubsteps = 10;
	    int leftIdx = (int)Mathf.Floor(curScale);
		for(int i = 0; i < numSubsteps && curScale < NumPoints - 1; ++i){
	    	float localT = curScale - leftIdx;
			curScale += travelDist / numSubsteps / (TangentAt(leftIdx, localT)).magnitude;
			leftIdx = (int)Mathf.Floor(curScale);
		}

		// return new point
		float clampedScale = Mathf.Min(NumPoints - 1.000001f, curScale);
		leftIdx = (int)Mathf.Floor(clampedScale);
		return EvalAt(leftIdx, clampedScale - leftIdx);
	}

	public float AngleAt(int startIdx, float t){
		if(startIdx >= NumPoints - 1){
			Debug.LogError("Invalid startIdx: " + startIdx + " in AngleAt");
		}

    	return BezierUtility.EvalAngle(points[startIdx].position, 
			points[startIdx].outTangent, points[startIdx + 1].inTangent,
			points[startIdx + 1].position, t);
	}


	public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){

		DynamicBuffer<PathPoint> buffer = dstManager.AddBuffer<PathPoint>(entity);
		foreach(Point p in points){
			buffer.Add(new PathPoint{
					position = p.position,
					inTangent = p.inTangent,
					outTangent = p.outTangent
				});
		}

		int timeIdx = TimePassedUtility.AddDefault(entity, dstManager);
		dstManager.AddComponentData(entity, new PathMovement{ 
				timeIdx = timeIdx,
				speed = speed
			});
	}
}

[InternalBufferCapacity(10)]
public struct PathPoint : IBufferElementData{
	public float3 position;
	public float3 inTangent;
	public float3 outTangent;
}

public struct PathMovement : IComponentData{
	public int timeIdx;
	public float speed;
}