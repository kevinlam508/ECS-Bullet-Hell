using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;               // IBufferElementData
using Unity.Mathematics;			// float3

[InternalBufferCapacity(5)]
public struct BulletHit : IBufferElementData
{
    public Entity bullet;
    public float3 hitPos;
}

[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class BulletHitProxy : MonoBehaviour, IConvertGameObjectToEntity{

    // copies monobehavior data into component data
    public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){

        dstManager.AddBuffer<BulletHit>(entity);
    }
}
