using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;                  // IComponentData, IConvertGameObjectToEntity
using Unity.Mathematics;               // math
using UnityEngine;
using UnityEngine.Assertions;          // Assert
using Unity.Rendering;                 // RenderMesh

[Serializable]
public struct AutoShoot : IComponentData{

    // timing data
	public float startDelay;
	public int started;
	public float period;

    // volley data
	public AutoShootSystem.ShotPattern pattern;
	public Entity bullet;
    public int count;
    public float angle; // radians
    public float centerAngle; // radians

    // buffer data
    public int timeIdx;
}

[RequiresEntityConversion]
public class AutoShootProxy : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{	

    // init cache and add to scene leaving events
    public static Dictionary<GameObject, Dictionary<BulletMovementData, Entity>> prefabCache;
    static AutoShootProxy(){
        prefabCache = new Dictionary<GameObject, Dictionary<BulletMovementData, Entity>>();
        SceneSwapper.OnSceneExit += prefabCache.Clear;
    }

    [Header("Timing")]
	[Tooltip("Time in seconds before the first fire")]
	public float startDelay;

	[Tooltip("Time in seconds between repeats")]
	public float period;

    [Header("Volley")]
	public AutoShootSystem.ShotPattern pattern;

    [Tooltip("Stats on the bullet's movement")]
    public BulletMovementData movementStats;

    [Tooltip("Base prefab of the bullet")]
    public GameObject bullet;

    [Tooltip("Number of bullets in this volley")]
    public int count;

    [Tooltip("Degrees, use depends on pattern")]
    public float angle;

    [Tooltip("Degrees counterclockwise from forward direction to offset pattern")]
    [Range(0, 360)]
    public float centerAngle;

    // Referenced prefabs have to be declared so that the conversion system knows about them ahead of time
    public void DeclareReferencedPrefabs(List<GameObject> gameObjects){
        gameObjects.Add(bullet);
    }

    private Entity GetBulletEntity(EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){
        Entity ent = Entity.Null;

        // already in cache, just get it
        if(prefabCache.ContainsKey(bullet) && prefabCache[bullet].ContainsKey(movementStats)){
            ent = prefabCache[bullet][movementStats];
        }
        // not in cache, make it and add to cache
        else{
            ent = (conversionSystem.HasPrimaryEntity(bullet)) 
                ? conversionSystem.GetPrimaryEntity(bullet)
                : conversionSystem.CreateAdditionalEntity(bullet);
            BulletMovement bm = movementStats.ToBulletMovement();
            dstManager.SetComponentData(ent, bm);
            dstManager.AddComponentData(ent, new Prefab());

            if(!prefabCache.ContainsKey(bullet)){
                prefabCache.Add(bullet, new Dictionary<BulletMovementData, Entity>());
            }
            prefabCache[bullet].Add(movementStats, ent);
        }

        return ent;
    }

    // Lets you convert the editor data representation to the entity optimal runtime representation
    public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){
        Assert.IsTrue(period > 0);

        // add a TimePassed component
        int timeIdx = TimePassedUtility.AddDefault(entity, dstManager);

        Entity bulletEnt =  GetBulletEntity(dstManager, conversionSystem);
        AutoShoot shootData = new AutoShoot
        {
            // The referenced prefab will already be converted due to DeclareReferencedPrefabs.
            // so just look up the entity version in the conversion system
            bullet = bulletEnt,
            startDelay = startDelay,
            started = 0,
            period = period,
            pattern = pattern,
            count = count,
            angle = math.radians(angle),
            centerAngle = math.radians(centerAngle),
            timeIdx = timeIdx
        };
        dstManager.AddComponentData(entity, shootData);

    }
}
