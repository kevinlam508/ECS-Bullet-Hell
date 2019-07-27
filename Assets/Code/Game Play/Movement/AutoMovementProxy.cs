using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;               // IComponentData, IConvertGameObjectToEntity
using Unity.Mathematics;			// math

[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class AutoMovementProxy : MonoBehaviour, IConvertGameObjectToEntity
{

	public BulletMovementSystem.MoveType moveType = default(BulletMovementSystem.MoveType);
    public float moveSpeed = 0;
    public float rotateSpeed = 0;

    public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){

        BulletMovement data;
        data = new BulletMovement { 
        	moveType = moveType,
			moveSpeed = moveSpeed,
			rotateSpeed = math.radians(rotateSpeed)
        };
        dstManager.AddComponentData(entity, data);
    }
}
