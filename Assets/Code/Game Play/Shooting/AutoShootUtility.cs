using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;                  // Entity
using Unity.Collections;			   // NativeHashMap

public static class AutoShootUtility
{
	// init caches and add to scene leaving events
    private static Dictionary<BulletMovementData, int> movementIdx;
    public static NativeList<BulletMovement> movementStatsCache;
    private static Dictionary<BulletDamageData, int> damageIdx;
    public static NativeList<BulletDamage> damageStatsCache;
    static AutoShootUtility(){
        movementIdx = new Dictionary<BulletMovementData, int>();
        damageIdx = new Dictionary<BulletDamageData, int>();
        SceneSwapper.OnSceneExit += ClearCaches;
    }

    public static void ClearCaches(){
    	movementIdx.Clear();
    	damageIdx.Clear();

    	if(damageStatsCache.IsCreated){
    		damageStatsCache.Dispose();
    	}
    	if(movementStatsCache.IsCreated){
    		movementStatsCache.Dispose();
    	}
    }

    // returns the index of the component version of the data in the cache
    public static int GetOrAddMovementIdx(BulletMovementData moveData){
    	if(!movementStatsCache.IsCreated){
    		movementStatsCache = new NativeList<BulletMovement>(Allocator.Persistent);
    	}

    	if(!movementIdx.ContainsKey(moveData)){
    		movementStatsCache.Add(moveData.ToBulletMovement());
    		movementIdx.Add(moveData, movementStatsCache.Length - 1);
    	}

    	return movementIdx[moveData];
    }

    // returns the index of the component version of the data in the cache
    public static int GetOrAddDamageIdx(BulletDamageData damageData){
    	if(!damageStatsCache.IsCreated){
    		damageStatsCache = new NativeList<BulletDamage>(Allocator.Persistent);
    	}

    	if(!damageIdx.ContainsKey(damageData)){
    		damageStatsCache.Add(damageData.ToBulletDamage());
    		damageIdx.Add(damageData, damageStatsCache.Length - 1);
    	}

    	return damageIdx[damageData];
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
