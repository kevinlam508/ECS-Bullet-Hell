using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// dots
using Unity.Mathematics;

public class Path : MonoBehaviour
{
	[Serializable]
	public class Point{
		public Vector3 position;
		public Vector3 inTangent;
		public Vector3 outTangent;
		public bool makeSmoothTangent;
		public int controlId;
		public bool isSelected;
	}

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
		if(idx > NumPoints){
			Debug.LogError(idx + " is out of bounds");
		}

		Point newPoint = new Point();
		// add to front
		if(idx == 0){
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
		points.Insert(idx, newPoint);
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
			Debug.LogError("Invalid startIdx in EvalAt");
		}

		return BezierUtility.EvalPoint(points[startIdx].position, 
			points[startIdx].outTangent, points[startIdx + 1].inTangent,
			points[startIdx + 1].position, t);
	}

	public Vector3 TangentAt(int startIdx, float t){
		if(startIdx >= NumPoints - 1){
			Debug.LogError("Invalid startIdx in EvalAt");
		}

    	return BezierUtility.EvalTangent(points[startIdx].position, 
			points[startIdx].outTangent, points[startIdx + 1].inTangent,
			points[startIdx + 1].position, t);
	}

	// curScale will contain the next scale to use 
	public Vector3 EvalConstantSpeed(ref float curScale, float dt){
		// approx ideal dt for desired speed
		float travelDist = speed * dt;
    	int leftIdx = (int)Mathf.Floor(curScale);
    	float localT = curScale - leftIdx;
		curScale += travelDist / (TangentAt(leftIdx, localT)).magnitude;

		// return new point
		float clampedScale = Mathf.Min(NumPoints - 1.1f, curScale);
		leftIdx = (int)Mathf.Floor(clampedScale);
		return EvalAt(leftIdx, clampedScale - leftIdx);
	}

	public float AngleAt(int startIdx, float t){
		if(startIdx >= NumPoints - 1){
			Debug.LogError("Invalid startIdx in AngleAt");
		}

    	return BezierUtility.EvalAngle(points[startIdx].position, 
			points[startIdx].outTangent, points[startIdx + 1].inTangent,
			points[startIdx + 1].position, t);
	}

	// utility for evaluating bezier related values
	public static class BezierUtility{
		// returns the point t percent along the curve with control points a,b,c,d
		public static float3 EvalPoint(float3 a, float3 b, float3 c, float3 d, float t){
			t = math.clamp(t, 0, 1);
			float revT = 1 - t;
			return (math.pow(revT, 3) * a)
				+ (3 * math.pow(revT, 2) * t * b)
				+ (3 * revT * math.pow(t, 2) * c)
				+ (math.pow(t, 3) * d);
		}

		// returns the tangent t percent along the curve with control points a,b,c,d
		public static float3 EvalTangent(float3 a, float3 b, float3 c, float3 d, float t){
			float3 v1 = (-3 * a) + (9 * b) + (-9 * c) + (3 * d);
	    	float3 v2 = (6 * a) + (-12 * b) + (6 * c);
	    	float3 v3 = (-3 * a) + (3 * b);
	    	return ((t * t * v1) + (t * v2) + v3);
		}

		// returns the angle of the tangent t percent along the curve with control points a,b,c,d
		//  the angle will be in radians
		public static float EvalAngle(float3 a, float3 b, float3 c, float3 d, float t){
			float3 tangent = EvalTangent(a, b, c, d, t);

			// edge cases
			if(tangent.x == 0){
				return (tangent.y > 0)? math.radians(90f) : math.radians(270f);
			}
			float angle = math.atan(tangent.y / tangent.x);

			// tangent is on other side, need to correct
			if(tangent.x < 0){
				angle += math.radians(180f);
			}
			return angle;
		}
	}
}
