using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Entities;

// dummy component to mark entities as enemy
[Serializable]
public struct Enemy : IComponentData{}

// monobehavior that will be converted into component using Convert()
[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class EnemyProxy : MonoBehaviour, IConvertGameObjectToEntity {
	public void Convert(Entity entity, EntityManager dstManager, 
			GameObjectConversionSystem conversionSystem){
        dstManager.AddComponentData(entity, new Enemy());
    }
}
