using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;

// stores read only info on how to move a bullet for performance
[Serializable]
public struct BulletMovement : IComponentData{
    public BulletMovementSystem.MoveType moveType;
    public float moveSpeed;
    public float rotateSpeed;
}

// old style component with inspector-editable values
//  will be converted into style component
[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class BulletMovementProxy : MonoBehaviour, IConvertGameObjectToEntity{
	
    public BulletMovementData stats;
    
    // copies monobehavior data into component data
    public void Convert(Entity entity, EntityManager dstManager, 
        GameObjectConversionSystem conversionSystem){
    	if(stats != null){
	        BulletMovement data = new BulletMovement { 
	        	moveType = stats.moveType,
				moveSpeed = stats.moveSpeed,
				rotateSpeed = stats.rotateSpeed
	        };
	        dstManager.AddComponentData(entity, data);
	    }
    }
}
