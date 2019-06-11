using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Entities;				// IComponentData, Entity, ...

[Serializable]
public struct PlayerShoot : IComponentData{
	public Entity bullet;
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

        dstManager.AddComponentData(entity, new PlayerShoot{
        		bullet = conversionSystem.GetPrimaryEntity(bullet),
        		shotCooldown = shotCooldown,
        		timeIdx = timeIdx
        	});
    }
}
