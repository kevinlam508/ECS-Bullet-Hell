using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Entities;               // IComponentData, IConvertGameObjectToEntity

public struct ExitMovement : IComponentData{
	public int timeIdx;
	public float exitTime;
	public float exitSpeed;
	public ExitMovementSystem.MovementComponent originalMovement;
}

[UnityEngine.DisallowMultipleComponent]
[RequiresEntityConversion]
public class ExitMovementProxy : MonoBehaviour, IConvertGameObjectToEntity
{

	[Tooltip("Amount of time after spawning to switch to exiting")]
	[SerializeField] private float exitTime = 0f;

	[Tooltip("Speed to exit at")]
	[SerializeField] private float exitSpeed = 1f;

    public void Convert(Entity entity, EntityManager dstManager, 
            GameObjectConversionSystem conversionSystem){
    	// ensure positive speed
    	Debug.Assert(exitSpeed > 0);

    	// determine starting movement type
    	ExitMovementSystem.MovementComponent type = ExitMovementSystem.MovementComponent.None;
    	if(gameObject.GetComponent<BulletMovementProxy>() != null){
    		type = ExitMovementSystem.MovementComponent.Simple;
    	}
    	else if(gameObject.GetComponent<PathMovementProxy>() != null){
    		type = ExitMovementSystem.MovementComponent.Path;
    	}

    	// add a component to track the time
    	int timeIdx = TimePassedUtility.AddDefault(entity, dstManager);

		dstManager.AddComponentData(entity, new ExitMovement{
				timeIdx = timeIdx,
				exitTime = exitTime,
				exitSpeed = exitSpeed,
				originalMovement = type
			});
	}
}
