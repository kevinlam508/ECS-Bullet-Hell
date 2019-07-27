using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Entities;				// IComponentData, Entity, ...
using Unity.Physics.Authoring;      // PhysicsShape
using Unity.Mathematics;            // float3

[Serializable]
public struct PlayerShoot : IComponentData{

    // bullet info
	public Entity bullet;
    public float initialScale;
    public float initialColliderRadius;

    // shooting info
	public float shotCooldown;
	public int timeIdx;
}

[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class PlayerShootProxy : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs {

	[SerializeField] private GameObject bullet = null;
	[SerializeField] private float shotCooldown = 1f;

	public void DeclareReferencedPrefabs(List<GameObject> gameObjects){
		// tell conversion system to also convert the bullet
        gameObjects.Add(bullet);
    }

	public void Convert(Entity entity, EntityManager dstManager, 
			GameObjectConversionSystem conversionSystem){

        // add a TimePassed component
        int timeIdx = TimePassedUtility.AddDefault(entity, dstManager);

        bullet.GetComponent<PhysicsShape>().GetCylinderProperties(out float3 center, 
            out float height, out float radius, out quaternion orientation);
        dstManager.AddComponentData(entity, new PlayerShoot{
        		bullet = conversionSystem.GetPrimaryEntity(bullet),
                initialScale = bullet.transform.localScale.x,
                initialColliderRadius = radius,
        		shotCooldown = shotCooldown,
        		timeIdx = timeIdx
        	});
    }
}
