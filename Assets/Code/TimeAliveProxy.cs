using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Entities;

[Serializable]
public struct TimeAlive : IComponentData
{
    [HideInInspector]
    public float time;
}

[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class TimeAliveProxy : MonoBehaviour, IConvertGameObjectToEntity
{
	public void Convert(Entity entity, EntityManager dstManager,
		GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new TimeAlive{time = 0});
    }
}
