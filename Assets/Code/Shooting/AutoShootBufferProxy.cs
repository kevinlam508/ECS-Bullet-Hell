using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;                  // IComponentData, IConvertGameObjectToEntity, IBufferElementData
using Unity.Mathematics;               // math
using UnityEngine.Assertions;          // Assert
using System;						   // Serializable

[DisallowMultipleComponent]
[RequiresEntityConversion]
public class AutoShootBufferProxy : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
	// TODO: implement nonmonohavior version of AutoShootProxy
	public List<AutoShootData> shots;

    public void DeclareReferencedPrefabs(List<GameObject> gameObjects){
    }

    public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){
    }
}
