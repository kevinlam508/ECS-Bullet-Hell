using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Entities;
using Unity.Jobs;

// empty components to mark for Bounary System
[Serializable]
public struct BoundMarkerEdge : IComponentData{}

[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class BoundMarkerEdgeProxy : MonoBehaviour, IConvertGameObjectToEntity{
    
    // add empty copy to attached entity
    public void Convert(Entity entity, EntityManager dstManager, 
        GameObjectConversionSystem conversionSystem){
        dstManager.AddComponentData(entity, new BoundMarkerEdge());
    }
}