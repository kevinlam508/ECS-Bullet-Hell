using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Assertions;

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
}

[RequiresEntityConversion]
public class AutoShootProxy : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{	

    [Header("Timing")]
	[Tooltip("Time in seconds before the first fire")]
	public float startDelay;

	[Tooltip("Time in seconds between repeats")]
	public float period;

    [Header("Volley")]
	public AutoShootSystem.ShotPattern pattern;

    [Tooltip("Stats on the bullet's movement")]
    public BulletMovementData movementStats;

    [Tooltip("Material of the bullet")]
    public Material bulletMaterial;

    [Tooltip("Number of bullets in this volley")]
    public int count;

    [Tooltip("Degrees, use depends on pattern")]
    public float angle;

    [Tooltip("Degrees counterclockwise from forward direction to offset pattern")]
    [Range(0, 360)]
    public float centerAngle;

    private GameObject bullet;

    // Referenced prefabs have to be declared so that the conversion system knows about them ahead of time
    // called before convert??? so bullet will be assigned properly
    public void DeclareReferencedPrefabs(List<GameObject> gameObjects){
        bullet = (GameObject)Resources.Load("Bullet");
        gameObjects.Add(bullet);
    }

    // Lets you convert the editor data representation to the entity optimal runtime representation
    public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){
        Assert.IsTrue(period > 0);

        // set bullet's movement stats
        bullet.GetComponent<BulletMovementProxy>().stats = movementStats;
        bullet.GetComponent<MeshRenderer>().material = bulletMaterial;

        AutoShoot shootData = new AutoShoot
        {
            // The referenced prefab will be converted due to DeclareReferencedPrefabs.
            // So here we simply map the game object to an entity reference to that prefab.
            bullet = conversionSystem.GetPrimaryEntity(bullet),
            startDelay = startDelay,
            started = 0,
            period = period,
            pattern = pattern,
            count = count,
            angle = math.radians(angle),
            centerAngle = math.radians(centerAngle)
        };
        dstManager.AddComponentData(entity, shootData);
    }
}
