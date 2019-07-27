using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;               // IComponentData, IConvertGameObjectToEntity

public struct Health : IComponentData{
	public int health;
}

[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class HealthProxy : MonoBehaviour, IConvertGameObjectToEntity{
    [SerializeField] private int maxHealth = 0;

    public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){

        dstManager.AddComponentData(entity, new Health{ health = maxHealth });
    }
}
