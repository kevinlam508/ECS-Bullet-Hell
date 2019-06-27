﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PathMovementProxy))]
public class PathInspector : Editor
{
    void OnSceneGUI(){
    	PathMovementProxy path = (PathMovementProxy)target;

		foreach(PathMovementProxy.Point p in path.points){
    		p.controlId = GUIUtility.GetControlID(FocusType.Passive);
		}
		switch(Event.current.type){
			case EventType.Repaint:
				DrawSelectionHandles(path);
				DrawCurve(path);
				break;
			case EventType.Layout:
				LayoutSelectionHandles(path);
				break;
			case EventType.MouseUp:
				UpdateSelected(path);
				break;
		}

		for(int i = 0; i < path.NumPoints; ++i){
			if(path[i].isSelected){
				UpdatePoint(path, i);
			}
		}
    }

    public override void OnInspectorGUI(){
        DrawDefaultInspector();

        PathMovementProxy path = (PathMovementProxy)target;

        GUILayout.Label("Point Controls", new GUIStyle(GUI.skin.label) {alignment = TextAnchor.MiddleCenter});

        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Select All")){
            path.SelectAll();
        }
        if(GUILayout.Button("Deselect All")){
        	path.ResetSelection();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Add to Front")){
	        Undo.RecordObject(path, "Added point to front");
            path.InsertPoint(0);
	        EditorUtility.SetDirty(path);
        }
        if(GUILayout.Button("Add to Back")){
	        Undo.RecordObject(path, "Added point to back");
            path.InsertPoint(path.NumPoints);
	        EditorUtility.SetDirty(path);
        }
        GUILayout.EndHorizontal();

        if(GUILayout.Button("Bisect") && path.HasSelected){
	        Undo.RecordObject(path, "Bisecting Curve");
            for(int i = path.NumPoints - 1; i > 0; --i){
            	if(path[i].isSelected && path[i - 1].isSelected){
            		path.InsertPoint(i);
	            	EditorUtility.SetDirty(path);
            	}
            }
        }
        if(GUILayout.Button("Delete Selected") && path.HasSelected){
	        Undo.RecordObject(path, "Deleting points");
            for(int i = path.NumPoints - 1; i > 0; --i){
            	if(path[i].isSelected){
            		path.DeletePoint(i);
	            	EditorUtility.SetDirty(path);
            	}
            }
        }
        if(GUILayout.Button("Duplicate Segment") && path.HasSelected){
            Undo.RecordObject(path, "duplicating points");
            path.DuplicateLongestSelectedSegment();
        }

        GUILayout.Label("Previewing", new GUIStyle(GUI.skin.label) {alignment = TextAnchor.MiddleCenter});

        if(GUILayout.Button(playing ? "Stop Playing" : "Start Playing")){
        	if(playing){
        		StopUpdate();
        	}
        	else{
        		StartUpdate();
        	}
        }
        
    }

    private static void DrawSelectionHandles(PathMovementProxy path){
    	for(int i = 0; i < path.NumPoints; ++i){
    		PathMovementProxy.Point p = path[i];

    		Handles.color = p.isSelected ? Color.blue : Color.white;

    		if(!p.makeSmoothTangent){
    			Handles.CubeHandleCap(
    				p.controlId,
    				p.position,
    				Quaternion.identity,
    				.3f,
    				EventType.Repaint);
    		}
    		else{
    			Handles.SphereHandleCap(
    				p.controlId,
    				p.position,
    				Quaternion.identity,
    				.3f,
    				EventType.Repaint);
    		}
    	}
    }

    private static void LayoutSelectionHandles(PathMovementProxy path){
    	for(int i = 0; i < path.NumPoints; ++i){
    		PathMovementProxy.Point p = path[i];

    		if(!p.makeSmoothTangent){
    			Handles.CubeHandleCap(
    				p.controlId,
    				p.position,
    				Quaternion.identity,
    				.3f,
    				EventType.Layout);
    		}
    		else{
    			Handles.SphereHandleCap(
    				p.controlId,
    				p.position,
    				Quaternion.identity,
    				.3f,
    				EventType.Layout);
    		}
    	}
    }

    private static void UpdateSelected(PathMovementProxy path){

        int id = HandleUtility.nearestControl;
        int changedIdx = -1;
		for(int i = 0; i < path.NumPoints && changedIdx == -1; ++i){
			if(path[i].controlId == id){
				if(path[i].isSelected){
					path[i].makeSmoothTangent = !path[i].makeSmoothTangent;
					if(path[i].makeSmoothTangent){
		            	path[i].inTangent = AlignPoints(path[i].outTangent,
		            		path[i].position, path[i].inTangent);
					}
				}
				else{
					path[i].isSelected = true;
					changedIdx = i;
				}

				Event.current.Use();
			}
		}

		for(int i = 0; changedIdx != -1 && i < path.NumPoints; ++i){
			if(!Event.current.control && changedIdx != i && path[i].isSelected){
				path[i].isSelected = false;
			}
		}
    }

    private static void UpdatePoint(PathMovementProxy path, int idx){

    	PathMovementProxy.Point p = path[idx];
    	Vector3 newPoint;

        // update inTangent
        if(idx > 0){
	        EditorGUI.BeginChangeCheck();
	        newPoint = Handles.PositionHandle(p.inTangent, Quaternion.identity);
	        if (EditorGUI.EndChangeCheck())
	        {
	            Undo.RecordObject(path, "Move In Tangent " + idx);
	            EditorUtility.SetDirty(path);
	            p.inTangent = newPoint;

	            // make all 3 points colinear
	            if(p.makeSmoothTangent){
	            	p.outTangent = AlignPoints(p.inTangent,
	            		p.position, p.outTangent);
	            }
	        }
	    }

        // update outTangent
        if(idx < path.points.Count - 1){
	        EditorGUI.BeginChangeCheck();
	        newPoint = Handles.PositionHandle(p.outTangent, Quaternion.identity);
	        if (EditorGUI.EndChangeCheck())
	        {
	            Undo.RecordObject(path, "Move Out Tangent " + idx);
	            EditorUtility.SetDirty(path);
	            p.outTangent = newPoint;

	            // make all 3 points colinear
	            if(p.makeSmoothTangent){
	            	p.inTangent = AlignPoints(p.outTangent,
	            		p.position, p.inTangent);
	            }
	        }
	    }

    	// update position
        EditorGUI.BeginChangeCheck();
        newPoint = Handles.PositionHandle(p.position, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(path, "Move Point " + idx);
            EditorUtility.SetDirty(path);

            // move all selected points
            Vector3 diff = newPoint - p.position;
            foreach(PathMovementProxy.Point point in path.points){
                if(point.isSelected){
                    MovePoint(point, diff);
                }
            }
        }
    }

    private static void MovePoint(PathMovementProxy.Point pt, Vector3 offset){
        pt.position += offset;
        pt.inTangent += offset;
        pt.outTangent += offset;
    }

    // returns vector with direction of pt1 to pt2 with maginitude of pt3 - pt2
    private static Vector3 AlignPoints(Vector3 pt1, Vector3 pt2, Vector3 pt3){
		Vector3 res = pt2 - pt1;
    	res.Normalize();
    	res *= (pt2 - pt3).magnitude;
    	res += pt2;

    	return res;
    }

    private static void DrawCurve(PathMovementProxy path){

    	Color[] colors = {
    		new Color(1, 0, 0), 
    		new Color(1, .5f, 0), 
    		new Color(1, 1, 0),
    		new Color(0, 1, 0), 
    		new Color(0, 0, 1), 
    		new Color(.15294f, 0, .2f),
    		new Color(.54509f, 0, 1)};

		for(int i = 0; i < path.NumPoints - 1; ++i){
			float scaledVal = (colors.Length - 1f) * i / path.NumPoints;
			int scaledIdx = (int)Mathf.Floor(scaledVal);
			Color startColor = colors[scaledIdx];
			Color endColor = colors[scaledIdx + 1];

			// draw curve between two adjacent points
			Handles.DrawBezier(path[i].position, 
				path[i + 1].position, 
				path[i].outTangent, 
				path[i + 1].inTangent, 
				Color.Lerp(startColor, endColor, scaledVal - scaledIdx), 
				null, 
				2f);
		}

		// draw lines to selected tangents
		for(int i = 0; i < path.NumPoints; ++i){
			if(path[i].isSelected){
				Handles.color = Color.gray;
				if(i < path.NumPoints - 1){
					Handles.DrawLine(path[i].position, path[i].outTangent);
				}
				if(i > 0){
					Handles.DrawLine(path[i].position, path[i].inTangent);
				}
			}
		}
    }

    // previewing path
    float time = 0;
    Transform trans = null;
    bool playing = false;
    Vector3 origPos;
    Quaternion origRot;
    void Update(){
    	PathMovementProxy path = ((PathMovementProxy)target);
    	if(time >= path.NumPoints - 1){
    		time = 0;
    	}    	
    	float radAngle = path.AngleAt((int)Mathf.Floor(time), time - Mathf.Floor(time));

    	// extra 90 degrees in rotation to make the forward vector front facing
    	trans.rotation = Quaternion.AngleAxis((Mathf.Rad2Deg * radAngle) - 90f, 
    		Vector3.forward);

    	trans.position = path.EvalConstantSpeed(ref time, Time.deltaTime);
    	trans.hasChanged = false;
    }

    private void StartUpdate(){
    	time = 0;
    	trans = ((PathMovementProxy)target).gameObject.transform;
    	origPos = trans.position;
    	origRot = trans.rotation;
    	playing = true;
    	EditorApplication.update += Update;
    }

    private void StopUpdate(){
    	trans.position = origPos;
    	trans.rotation = origRot;
    	trans = null;
    	playing = false;
    	EditorApplication.update -= Update;
    }

    void OnDisable(){
    	if(playing){
    		StopUpdate();
    	}
    }
}
