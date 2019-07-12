using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Entities;				// Entity
public class ParallaxManager : MonoBehaviour
{
	[SerializeField] private float topBound = 0;
	[SerializeField] private float bottomBound = 0;
	[SerializeField] private float leftBound = 0;
	[SerializeField] private float rightBound = 0;
	[SerializeField] private List<ParallaxLayer> layers = new List<ParallaxLayer>();

	[System.Serializable]
    private class ParallaxLayer{
    	public GameObject prefab = null;
    	public float minVelocity = 0;
    	public float maxVelocity = 0;

    	[Tooltip("The z value of the particle.")]
    	public float height = 0;

    	[Tooltip("The number of objects on this layer.")]
    	public int count = 0;
    }

    void Awake(){
    	ParallaxSystem ps = World.Active.GetOrCreateSystem<ParallaxSystem>();
    	ps.SetMovementBounds(topBound, bottomBound);
    	ps.SetSpawnBounds(leftBound, rightBound);
    	foreach(ParallaxLayer layer in layers){
    		if(layer.prefab != null){
	    		ps.AddLayer(layer.prefab, layer.count, layer.minVelocity, 
	    				layer.maxVelocity, layer.height);
	    	}
    	}
    }
}
