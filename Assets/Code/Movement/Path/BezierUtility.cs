using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

// utility for evaluating bezier related values
public static class BezierUtility{

	public static float circleConst = 0.55191502449f;

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

	public static bool HasLoop(int loopIndex, int length){
		return loopIndex >= 0 && loopIndex < length - 1;
	}

	// returns wether a value is in bound based on the loop index and length
	public static bool IsInBound(float val, int loopIndex, int length){
		return (val < length - 1 && !HasLoop(loopIndex, length)) 
			|| (val < length && HasLoop(loopIndex, length));
	}
}
