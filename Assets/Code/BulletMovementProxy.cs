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
public struct BulletMovement : IComponentData
{
    public BulletMovementSystem.MoveType moveType;
    public float moveSpeed;
    public float rotateSpeed;

    // inital state of bullet
    public BulletMovementSystem.InitState init;
    public float3 initPos;
    public float3 initalPos;
    public quaternion initRot;

}

// old style component with inspector-editable values
//  will be converted into style component
[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class BulletMovementProxy : MonoBehaviour, IConvertGameObjectToEntity
{
	[SerializeField]
    private BulletMovementSystem.MoveType moveType = BulletMovementSystem.MoveType.LINEAR;

    [Tooltip("Movement in pixels per second")]
    [SerializeField]
    private float moveSpeed = 1;
    [Tooltip("Rotation in degrees per second")]
	[SerializeField]
    private float rotateSpeed = 0;
    
    // copies monobehavior data into component data
    public void Convert(Entity entity, EntityManager dstManager, 
        GameObjectConversionSystem conversionSystem)
    {
        BulletMovement data = new BulletMovement { 
        	moveType = moveType,
			moveSpeed = moveSpeed,
			rotateSpeed = rotateSpeed,
			init = BulletMovementSystem.InitState.NOT_INIT
        };
        dstManager.AddComponentData(entity, data);
    }
}
