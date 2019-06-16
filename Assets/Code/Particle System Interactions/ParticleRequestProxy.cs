using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;               // IComponentData

public struct ParticleRequest : IComponentData{
	public ParticleRequestSystem.ParticleType type;
}

public class ParticleRequestProxy : MonoBehaviour, IConvertGameObjectToEntity{

	ParticleRequestSystem.ParticleType type;

	public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){

        dstManager.AddComponentData(entity, new ParticleRequest{
        		type = type
        	});
    }
}
