using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

// build based on raycastjob in VehicleMachanics.cs

public struct BulletHit : IComponentData
{
}

[UnityEngine.DisallowMultipleComponent]
public class BulletHitProxy : MonoBehaviour, IConvertGameObjectToEntity {
	public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new BulletHit());
    }
}