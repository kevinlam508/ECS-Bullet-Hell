using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;                  // IComponentData, IConvertGameObjectToEntity, IBufferElementData
using Unity.Mathematics;               // math
using UnityEngine;
using UnityEngine.Assertions;          // Assert
using Unity.Rendering;                 // RenderMesh

[Serializable]
public struct AutoShoot : IComponentData{

    // timing data
	public float startDelay;
	public float period;
    public float cooldownDuration;

    // volley data
    public float3 sourceOffset;
	public AutoShootSystem.ShotPattern pattern;
	public Entity bullet;
    public int count;
    public int numVolleys;
    public float angle; // radians
    public AutoShootSystem.AimStyle aimStyle;
    public float centerAngle; // radians

    // buffer data
    public int timeIdx;
    public int volleyCountIdx;

    // bullet stats data
    public int moveStatsIdx;
    public int damageStatsIdx;
}

[Serializable]
public struct AutoShootData{
    [Header("Timing")]
    [Tooltip("Time in seconds before the first fire")]
    public float startDelay;
    [Tooltip("Time in seconds between volleys")]
    public float period;
    [Tooltip("Time in seconds to wait after firing a set of volleys")]
    public float cooldownDuration;

    [Header("Volley")]
    [Tooltip("Offset from the shooter's center for where the bullets will spawn")]
    public Vector2 sourceOffset;
    public AutoShootSystem.ShotPattern pattern;
    [Tooltip("Stats on the bullet's movement")]
    public BulletMovementData movementStats;
    [Tooltip("Stats on the bullet's damage")]
    public BulletDamageData damageStats;
    [Tooltip("Base prefab of the bullet")]
    public GameObject bullet;
    [Tooltip("Number of bullets in this volley")]
    public int count;
    [Tooltip("Number of volleys shot before cooldown")]
    public int numVolleys;
    [Tooltip("Degrees, use depends on pattern")]
    public float angle;

    [Header("Aiming")]
    [Tooltip("How to aim the volley")]
    public AutoShootSystem.AimStyle aimStyle;
    [Tooltip("Degrees counterclockwise from aimed direction to offset pattern")]
    [Range(0, 360)]
    public float centerAngle;

    // Referenced prefabs have to be declared so that the conversion system knows about them ahead of time
    public void DeclareReferencedPrefabs(List<GameObject> gameObjects){
        gameObjects.Add(bullet);
    }

    // Lets you convert the editor data representation to the entity optimal runtime representation
    public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){
        Assert.IsTrue(period > 0);

        // add a TimePassed component
        int timeIdx = TimePassedUtility.AddDefault(entity, dstManager);
        int volleyCountIdx = TimePassedUtility.AddDefault(entity, dstManager);

        Entity bulletEnt = conversionSystem.GetPrimaryEntity(bullet);
        AutoShoot shootData = new AutoShoot
        {
            // The referenced prefab will already be converted due to DeclareReferencedPrefabs.
            // so just look up the entity version in the conversion system
            bullet = bulletEnt,
            startDelay = startDelay,
            cooldownDuration = cooldownDuration,
            period = period,
            pattern = pattern,
            count = count,

            sourceOffset = new float3(sourceOffset.x, sourceOffset.y, 0),
            numVolleys = numVolleys,
            angle = math.radians(angle),
            aimStyle = aimStyle,
            centerAngle = math.radians(centerAngle),

            timeIdx = timeIdx,
            volleyCountIdx = volleyCountIdx,

            moveStatsIdx = AutoShootUtility.GetOrAddMovementIdx(movementStats),
            damageStatsIdx = AutoShootUtility.GetOrAddDamageIdx(damageStats)
        };

        DynamicBuffer<AutoShootBuffer> buffer = AutoShootUtility.GetOrAddBuffer(entity, dstManager);
        buffer.Add(new AutoShootBuffer{ val = shootData });
    }
}

public struct AutoShootBuffer : IBufferElementData{
    public AutoShoot val;
}

[DisallowMultipleComponent]
[RequiresEntityConversion]
public class AutoShootProxy : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{	
    public List<AutoShootData> patterns;

    // Referenced prefabs have to be declared so that the conversion system knows about them ahead of time
    public void DeclareReferencedPrefabs(List<GameObject> gameObjects){
        foreach(AutoShootData data in patterns){
            data.DeclareReferencedPrefabs(gameObjects);
        }
    }

    // Lets you convert the editor data representation to the entity optimal runtime representation
    public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){
        foreach(AutoShootData data in patterns){
            data.Convert(entity, dstManager, conversionSystem);
        }
    }

}
