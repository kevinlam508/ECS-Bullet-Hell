using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Adjusts the position of a UI element's anchors to where reference is
 */
[ExecuteInEditMode]
public class SidePanelAdjust : MonoBehaviour
{
	public enum Direction { Top, Right, Bottom, Left}

    [SerializeField] private Transform reference = null;
    [SerializeField] private Direction sideToAdjust = Direction.Top;
    private RectTransform rect;

    void Awake(){
    	rect = GetComponent<RectTransform>();
    }

    void Update(){
    	Vector3 screenPos = Camera.main.WorldToScreenPoint(reference.position);
    	float newAnchor = RelativePos(screenPos, sideToAdjust);
    	switch(sideToAdjust){
			case Direction.Top:
				rect.anchorMax = new Vector2(rect.anchorMax.x, newAnchor);
				break;
    		case Direction.Bottom:
				rect.anchorMin = new Vector2(rect.anchorMin.x, newAnchor);
    			break;
    		case Direction.Right:
				rect.anchorMax = new Vector2(newAnchor, rect.anchorMax.y);
    			break;
    		case Direction.Left:
				rect.anchorMin = new Vector2(newAnchor, rect.anchorMin.y);
    			break;
    	}
    }

    private float RelativePos(Vector3 screenPos, Direction side){
    	switch(side){
    		case Direction.Top:
    		case Direction.Bottom:
    			return screenPos.y / Screen.height;
    		case Direction.Right:
    		case Direction.Left:
    			return screenPos.x / Screen.width;
    	}

    	return 0;
    }
}
