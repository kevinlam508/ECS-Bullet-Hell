using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;                       // Serializable
using Unity.Entities;               // IComponentData, IConvertGameObjectToEntity
using Unity.Mathematics;            // math

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
    
    public BulletMovementData stats = null;

    // copies monobehavior data into component data
    public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){

        BulletMovement data;

        // stats exist, prefill
        if(stats != null){
            data = stats.ToBulletMovement();
        }
        // no stats, make default
        else{
            data = new BulletMovement { 
            	moveType = BulletMovementSystem.MoveType.LINEAR,
    			moveSpeed = 0,
    			rotateSpeed = 0
            };
        }
        dstManager.AddComponentData(entity, data);
    }
}
