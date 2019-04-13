using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;

// dummy component to mark entities as player
[Serializable]
public struct Player : IComponentData{}

// monobehavior that will be converted into component using Convert()
[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class PlayerProxy : MonoBehaviour, IConvertGameObjectToEntity {
	public void Convert(Entity entity, EntityManager dstManager, 
			GameObjectConversionSystem conversionSystem){
        dstManager.AddComponentData(entity, new Player());
    }
}
