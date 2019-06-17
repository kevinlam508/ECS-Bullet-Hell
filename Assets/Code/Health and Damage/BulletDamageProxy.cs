using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;               // IComponentData, IConvertGameObjectToEntity

public struct BulletDamage : IComponentData{
    public int damage;
}

[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class BulletDamageProxy : MonoBehaviour, IConvertGameObjectToEntity{
    
    public int damage = 1;

    // copies monobehavior data into component data
    public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){

        dstManager.AddComponentData(entity, new BulletDamage{ damage = damage });
    }
}
