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

	[Tooltip("The point to go to after reaching the end, a negative number or a number beyond the number of points means no looping")]
	public int loopIndex = -1;
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

	public bool HasLoop{
		get{
			return loopIndex >= 0 && loopIndex < NumPoints;
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

	/*
	 * if there are at least two points selected, makes a circle where:
	 *    order of points on circle : rotation that causes the least change to 
	 *        the outTangent of the first idx
	 *    radius : average of the distances from the points and the center point
	 *    center : 
	 *		  2 points: the missing corners of the rectangle of the points
	 *        3 points: the average position of the first and last points
	 *        4+ points: the average position of all points
	 */		
	public void MakeCircleFromLongestSegment(){
        // find the longest selected segment
        int segmentLength = 1;
        int idx = 0;
        FindLongestSegment(ref idx, ref segmentLength);

		// determine relative positions
		Vector3 firstToSecond = points[idx + 1].position - points[idx].position;
		Direction relX = (firstToSecond.x > 0) ? Direction.Right : Direction.Left;
		Direction relY = (firstToSecond.y > 0) ? Direction.Up : Direction.Down;

		// determine direction of rotation
		Vector3 sideCheck = -Vector3.Cross(firstToSecond, Vector3.forward);
		Rotation rot = (Vector3.Dot(sideCheck, points[idx].outTangent - points[idx].position) > 0)
			? Rotation.Clockwise : Rotation.CounterClockwise;

		Direction firstToCenterDir = Direction.Up;
		Vector3 center = Vector3.zero;
        switch(segmentLength){
        	case -1:	// none found, do nothing
        	case 0:		// listed 0 and 1 in case of mistake
        	case 1:
        		return;
        	case 2:
        		Get2PtCircleCenter(idx, relX, relY, rot, out firstToCenterDir, out center);
        		break;
        	case 3:
        		Get3PtCircleCenter(idx, out firstToCenterDir, out center);
        		break;
        	default:
        		GetNPtCircleCenteR(idx, segmentLength, out firstToCenterDir, out center);
        		break;
        }

		// average the radius
		float radius = 0;
		for(int i = 0; i < segmentLength; ++i){
			radius += (points[i + idx].position - center).magnitude;
		}
		radius /= segmentLength;

		AlignPointsToCircle(idx, segmentLength, center, firstToCenterDir, rot, radius);
	}

	enum Direction{ Up, Right, Down, Left }
	enum Rotation{ Clockwise, CounterClockwise }

	// gets the center for the two points of a circle
	private void Get2PtCircleCenter(int idx, Direction relX, Direction relY,
			Rotation rot, out Direction toCenterDir, out Vector3 centerPos){
		Vector3 firstToSecond = points[idx + 1].position - points[idx].position;

		// determine where the center is
		toCenterDir = GetCenterDir(relX, relY, rot);
		centerPos = points[idx].position;
		switch(toCenterDir){
			case Direction.Up:
			case Direction.Down:
				centerPos.y += firstToSecond.y;
				break;
			case Direction.Right:
			case Direction.Left:
				centerPos.x += firstToSecond.x;
				break;
		}
	}

	// center is average of first and third points
	private void Get3PtCircleCenter(int idx, out Direction toCenterDir, 
			out Vector3 centerPos){
		centerPos = (points[idx].position + points[idx + 2].position) / 2;

		// direction is along axis with strongest maginitude
		Vector3 firstToCenter = centerPos - points[idx].position;
		if(Mathf.Abs(firstToCenter.x) >  Mathf.Abs(firstToCenter.y)){
			if(firstToCenter.x > 0){
				toCenterDir = Direction.Right;
			}
			else{
				toCenterDir = Direction.Left;
			}
		}
		else{
			if(firstToCenter.y > 0){
				toCenterDir = Direction.Up;
			}
			else{
				toCenterDir = Direction.Down;
			}
		}
	}

	// center is average of first 4 points
	private void GetNPtCircleCenteR(int idx, int numPoints, out Direction toCenterDir, 
			out Vector3 centerPos){

		// get average
		centerPos = Vector3.zero;
		for(int i = 0; i < 4; ++i){
			centerPos += points[i + idx].position;
		}
		centerPos /= 4;

		// direction is along axis with strongest maginitude
		Vector3 firstToCenter = centerPos - points[idx].position;
		if(Mathf.Abs(firstToCenter.x) >  Mathf.Abs(firstToCenter.y)){
			if(firstToCenter.x > 0){
				toCenterDir = Direction.Right;
			}
			else{
				toCenterDir = Direction.Left;
			}
		}
		else{
			if(firstToCenter.y > 0){
				toCenterDir = Direction.Up;
			}
			else{
				toCenterDir = Direction.Down;
			}
		}
	}

	// makes a circle from the given params
	private void AlignPointsToCircle(int startIdx, int numPoints, Vector3 center,
			Direction firstToCenterDir, Rotation rot, float radius){

		Direction ptDirection = (Direction)(((int)firstToCenterDir + 2) % 4);
		int nextDirOffset = (rot == Rotation.Clockwise) ? 1 : 3;
		for(int i = 0; i < numPoints; ++i){
			Point curPoint = points[startIdx + i];
			curPoint.makeSmoothTangent = true;

			// update position
			switch(ptDirection){
				case Direction.Up:
					curPoint.position = center + (Vector3.up * radius);
					break;
				case Direction.Left:
					curPoint.position = center + (Vector3.left * radius);
					break;
				case Direction.Down:
					curPoint.position = center + (Vector3.down * radius);
					break;
				case Direction.Right:
					curPoint.position = center + (Vector3.right * radius);
					break;
			}

			// update outTangent, it's direction is the next one in the rotation
			switch((Direction)(((int)ptDirection + nextDirOffset) % 4)){
				case Direction.Up:
					curPoint.outTangent = curPoint.position + (Vector3.up 
						* radius * BezierUtility.circleConst);
					break;
				case Direction.Left:
					curPoint.outTangent = curPoint.position + (Vector3.left 
						* radius * BezierUtility.circleConst);
					break;
				case Direction.Down:
					curPoint.outTangent = curPoint.position + (Vector3.down 
						* radius * BezierUtility.circleConst);
					break;
				case Direction.Right:
					curPoint.outTangent = curPoint.position + (Vector3.right 
						* radius * BezierUtility.circleConst);
					break;
			}

			// update next's inTangent if in bounds
			// direction of inTangent is next next one in rotation
			if(i + 1 < numPoints){
				int idx = startIdx + i + 1;
				switch(ptDirection){
					case Direction.Up:
						points[idx].inTangent = points[idx].position 
							+ (Vector3.up * radius * BezierUtility.circleConst);
						break;
					case Direction.Left:
						points[idx].inTangent = points[idx].position 
							+ (Vector3.left * radius * BezierUtility.circleConst);
						break;
					case Direction.Down:
						points[idx].inTangent = points[idx].position 
							+ (Vector3.down * radius * BezierUtility.circleConst);
						break;
					case Direction.Right:
						points[idx].inTangent = points[idx].position 
							+ (Vector3.right * radius * BezierUtility.circleConst);
						break;
				}
			}

			// update direction depending on rotation
			ptDirection = (Direction)(((int)ptDirection + nextDirOffset) % 4);
		}

		// first and last points may not have smooth tangents
		points[startIdx].makeSmoothTangent = points[startIdx + numPoints  - 1].makeSmoothTangent 
			= false;
	}

	// returns up if invalid, otherwise the direction to to the center relative
	//    to the first point
	private Direction GetCenterDir(Direction relX, Direction relY, Rotation rot){
		if(rot == Rotation.Clockwise){
			if(relX == Direction.Right && relY == Direction.Down){
				return Direction.Down;
			}
			else if(relX == Direction.Left && relY == Direction.Down){
				return Direction.Left;
			}
			else if(relX == Direction.Left && relY == Direction.Up){
				return Direction.Up;
			}
			else if(relX == Direction.Right && relY == Direction.Up){
				return Direction.Right;
			}
			else{
				return Direction.Up;
			}
		}
		else{
			if(relX == Direction.Right && relY == Direction.Down){
				return Direction.Right;
			}
			else if(relX == Direction.Left && relY == Direction.Down){
				return Direction.Down;
			}
			else if(relX == Direction.Left && relY == Direction.Up){
				return Direction.Left;
			}
			else if(relX == Direction.Right && relY == Direction.Up){
				return Direction.Up;
			}
			else{
				return Direction.Up;
			}
		}
	}

	// reflects selected points across x = 0
	public void FlipOnGlobalX(){
		foreach(Point p in points){
			if(p.isSelected){
				p.position.x = -p.position.x;
				p.inTangent.x = -p.inTangent.x;
				p.outTangent.x = -p.outTangent.x;
			}
		}
	}

	// reflects selected points across y = 0
	public void FlipOnGlobalY(){
		foreach(Point p in points){
			if(p.isSelected){
				p.position.y = -p.position.y;
				p.inTangent.y = -p.inTangent.y;
				p.outTangent.y = -p.outTangent.y;
			}
		}
	}

	// reflects selected points across average x, excludes tangents in average
	public void FlipOnLocalX(){
		// determine average
		float average = 0;
		int count = 0;
		foreach(Point p in points){
			if(p.isSelected){
				average += p.position.x;
				++count;
			}
		}
		average /= count;

		// move center points on (0, 0, 0)
		foreach(Point p in points){
			if(p.isSelected){
				p.position.x -= average;
				p.inTangent.x -= average;
				p.outTangent.x -= average;
			}
		}

		// flip on global
		FlipOnGlobalX();

		// move points back to original center
		foreach(Point p in points){
			if(p.isSelected){
				p.position.x += average;
				p.inTangent.x += average;
				p.outTangent.x += average;
			}
		}
	}

	// reflects selected points across average y, excludes tangents in average
	public void FlipOnLocalY(){
		// determine average
		float average = 0;
		int count = 0;
		foreach(Point p in points){
			if(p.isSelected){
				average += p.position.y;
				++count;
			}
		}
		average /= count;

		// move center points on (0, 0, 0)
		foreach(Point p in points){
			if(p.isSelected){
				p.position.y -= average;
				p.inTangent.y -= average;
				p.outTangent.y -= average;
			}
		}

		// flip on global
		FlipOnGlobalY();

		// move points back to original center
		foreach(Point p in points){
			if(p.isSelected){
				p.position.y += average;
				p.inTangent.y += average;
				p.outTangent.y += average;
			}
		}
	}

	// swaps the sides the tangents are on
	public void FlipTangents(){
		foreach(Point p in points){
			if(p.isSelected){
				Vector3 posToOut = p.outTangent - p.position;
				Vector3 posToIn = p.inTangent - p.position;
				p.outTangent = p.position - posToIn;
				p.inTangent = p.position - posToOut;
			}
		}
	}

	// returns the point on the curve between startIdx and startIdx + 1
	public Vector3 EvalAt(int startIdx, float t){
		if(!HasLoop && startIdx >= NumPoints - 1){
			Debug.LogError("Invalid startIdx: " + startIdx + " in EvalAt");
		}
		else if(HasLoop && startIdx >= NumPoints){
			Debug.LogError("Invalid startIdx: " + startIdx + " in EvalAt");
		}

		if(HasLoop && startIdx == NumPoints - 1){
			return BezierUtility.EvalPoint(points[startIdx].position, 
				points[startIdx].outTangent, points[loopIndex].inTangent,
				points[loopIndex].position, t);
		}
		return BezierUtility.EvalPoint(points[startIdx].position, 
			points[startIdx].outTangent, points[startIdx + 1].inTangent,
			points[startIdx + 1].position, t);
	}

	// returns the tangent at the poitn on the curve between startIdx and startIdx + 1
	public Vector3 TangentAt(int startIdx, float t){
		if(!HasLoop && startIdx >= NumPoints - 1){
			Debug.LogError("Invalid startIdx: " + startIdx + " in TangentAt");
		}
		else if(HasLoop && startIdx >= NumPoints){
			Debug.LogError("Invalid startIdx: " + startIdx + " in TangentAt");
		}

		if(HasLoop && startIdx == NumPoints - 1){
			return BezierUtility.EvalTangent(points[startIdx].position, 
				points[startIdx].outTangent, points[loopIndex].inTangent,
				points[loopIndex].position, t);
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
		for(int i = 0; i < numSubsteps && ((curScale < NumPoints - 1 && !HasLoop) || (curScale < NumPoints && HasLoop)); ++i){
	    	float localT = curScale - leftIdx;
			curScale += travelDist / numSubsteps / (TangentAt(leftIdx, localT)).magnitude;
			leftIdx = (int)Mathf.Floor(curScale);
		}

		// return new point
		float evalT = 0;
		int evalIdx = -1;
		// at last point for non loop, stay at last point
		if(!HasLoop && curScale >= NumPoints - 1){
			evalT = 1f;
			evalIdx = NumPoints - 2;
		}
		// past last point for loop, go towards loop index
		else if(HasLoop && curScale >= NumPoints){
			evalT = 1f;
			evalIdx = NumPoints - 1;
		}
		// normal case
		else{
			evalIdx = (int)Mathf.Floor(curScale);
			evalT = curScale - evalIdx;
		}
		return EvalAt(evalIdx, evalT);
	}

	public float AngleAt(int startIdx, float t){
		if(!HasLoop && startIdx >= NumPoints - 1){
			Debug.LogError("Invalid startIdx: " + startIdx + " in AngleAt");
		}
		else if(HasLoop && startIdx >= NumPoints){
			Debug.LogError("Invalid startIdx: " + startIdx + " in AngleAt");
		}

		if(HasLoop && startIdx == NumPoints - 1){
			return BezierUtility.EvalAngle(points[startIdx].position, 
				points[startIdx].outTangent, points[loopIndex].inTangent,
				points[loopIndex].position, t);
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
				speed = speed,
				loopIndex = loopIndex
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
	public int loopIndex;
}