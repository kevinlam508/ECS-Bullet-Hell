using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;               // IComponentData, IConvertGameObjectToEntity

public struct BulletDamage : IComponentData{
    public int damage;
    public int pierceCount;
}

[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class BulletDamageProxy : MonoBehaviour, IConvertGameObjectToEntity{
    
    public BulletDamageData damageStats = null;

    // copies monobehavior data into component data
    public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){

    	// default damage
    	if(damageStats == null){
        	dstManager.AddComponentData(entity, new BulletDamage{ 
        			damage = 1,
        			pierceCount = 0
        		});
        }
        else{
        	dstManager.AddComponentData(entity, damageStats.ToBulletDamage());
        }
    }
}
