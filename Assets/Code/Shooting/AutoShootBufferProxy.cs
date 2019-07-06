using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;                  // IComponentData, IConvertGameObjectToEntity, IBufferElementData

[DisallowMultipleComponent]
[RequiresEntityConversion]
public class AutoShootBufferProxy : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
	// TODO: implement nonmonohavior version of AutoShootProxy
	// [SerializeField]
	// public List<AutoShootProxy> shots;

 //    public void DeclareReferencedPrefabs(List<GameObject> gameObjects){
 //        foreach(AutoShootProxy shoot in shots){
 //        	shoot.DeclareReferencedPrefabs(gameObjects);
 //        }
 //    }

 //    public void Convert(Entity entity, EntityManager dstManager, 
 //            GameObjectConversionSystem conversionSystem){
 //        foreach(AutoShootProxy shoot in shots){
 //        	shoot.Convert(entity, dstManager, conversionSystem);
 //        }
 //    }
}
