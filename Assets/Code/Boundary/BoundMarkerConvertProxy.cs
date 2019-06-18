using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Entities;
using Unity.Jobs;

[Serializable]
public struct BoundMarkerConvert : IComponentData{
	public BoundarySystem.InteractionType newMarker;
}

[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class BoundMarkerConvertProxy : MonoBehaviour, IConvertGameObjectToEntity{
    
	[SerializeField] private BoundarySystem.InteractionType newMarker = default(BoundarySystem.InteractionType);

    // add empty copy to attached entity
    public void Convert(Entity entity, EntityManager dstManager, 
        GameObjectConversionSystem conversionSystem){
        dstManager.AddComponentData(entity, new BoundMarkerConvert{
        		newMarker = newMarker
        	});
    }
}
