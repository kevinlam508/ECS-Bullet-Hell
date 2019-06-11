using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;				// IComponentData, Entity, ...
using System;						// Serializable

// dummy component to mark entities as enemy bullet
[Serializable]
public struct EnemyBullet : IComponentData{}

// monobehavior that will be converted into component using Convert()
[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class EnemyBulletProxy : MonoBehaviour, IConvertGameObjectToEntity {
	public void Convert(Entity entity, EntityManager dstManager, 
			GameObjectConversionSystem conversionSystem){
        dstManager.AddComponentData(entity, new EnemyBullet());
    }
}
