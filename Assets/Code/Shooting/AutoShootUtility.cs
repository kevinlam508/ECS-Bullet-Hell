using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;                  // Entity

public static class AutoShootUtility
{
	// init cache and add to scene leaving events
    private static Dictionary<GameObject, Dictionary<BulletMovementData, Dictionary<BulletDamageData, Entity>>> prefabCache;
    static AutoShootUtility(){
        prefabCache = new Dictionary<GameObject, Dictionary<BulletMovementData, Dictionary<BulletDamageData, Entity>>>();
        SceneSwapper.OnSceneExit += prefabCache.Clear;
    }

    // returns the entity in the cache, returns Entity.Null if not in the cache
    public static Entity GetBullet(GameObject bullet, BulletMovementData moveData, 
    			BulletDamageData damageData){
    	if(prefabCache.ContainsKey(bullet)){
			Dictionary<BulletMovementData, Dictionary<BulletDamageData, Entity>> moveCache
				= prefabCache[bullet];
			if(moveCache.ContainsKey(moveData)){
				Dictionary<BulletDamageData, Entity> damageCache = moveCache[moveData];
				if(damageCache.ContainsKey(damageData)){
					return damageCache[damageData];
				}
			}
    	}
    	return Entity.Null;
    }

    public static void AddBullet(GameObject bullet, BulletMovementData moveData, 
    			BulletDamageData damageData, Entity val){

    	// get movement cache
    	Dictionary<BulletMovementData, Dictionary<BulletDamageData, Entity>> moveCache = null;
		if(prefabCache.ContainsKey(bullet)){
        	moveCache = prefabCache[bullet];
        }
        else{
			moveCache = new Dictionary<BulletMovementData, Dictionary<BulletDamageData, Entity>>();
            prefabCache.Add(bullet, moveCache);
        }

        // get damage cache
        Dictionary<BulletDamageData, Entity> damageCache = null;
        if(moveCache.ContainsKey(moveData)){
        	damageCache = moveCache[moveData];
        }
        else{
        	damageCache =  new Dictionary<BulletDamageData, Entity>();
            moveCache.Add(moveData, damageCache);
        }

        // add to lowest level cache
        damageCache.Add(damageData, val);
    }

    public static DynamicBuffer<AutoShootBuffer> GetOrAddBuffer(Entity ent, EntityManager manager){
        if(manager.HasComponent<AutoShootBuffer>(ent)){
            return manager.GetBuffer<AutoShootBuffer>(ent);
        }
        else{
            return manager.AddBuffer<AutoShootBuffer>(ent);
        }
    }
}
