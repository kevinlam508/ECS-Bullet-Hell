using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Entities;				// Entity
using Unity.Jobs;					// IcomponentData
using Unity.Transforms;				// Translation
using Unity.Collections;			// ReadOnly
using Unity.Mathematics;

using Random = Unity.Mathematics.Random;

public struct ParallaxMovement : IComponentData{
	public float minVelocity;
	public float maxVelocity;
	public float currentVelocity;
}

[SystemType(ActiveSystemManager.SystemTypes.VisualEffect)]
public class ParallaxSystem : JobComponentSystem
{

	private EntityQuery parallaxParticles;
	private float topBound, bottomBound;
	private float leftBound, rightBound;

	// sets the veritcal limits of where particles can be
	public void SetMovementBounds(float topBound, float bottomBound){
		this.topBound = topBound;
		this.bottomBound = bottomBound;
	}

	// sets the horizontal limits of where particles can be
	public void SetSpawnBounds(float leftBound, float rightBound){
		this.leftBound = leftBound;
		this.rightBound = rightBound;
	}

	public void AddLayer(GameObject prefab, int count, float minVelocity, 
			float maxVelocity, float height){
		Entity ent = GameObjectConversionUtility.ConvertGameObjectHierarchy(
			prefab, World.Active);
		for(int i = 0; i < count; ++i){
			Entity newEnt = EntityManager.Instantiate(ent);
			EntityManager.SetComponentData(newEnt, new Translation{
					Value = new float3(UnityEngine.Random.Range(leftBound, rightBound),
						UnityEngine.Random.Range(topBound, bottomBound),
						height)
				});
			EntityManager.AddComponentData(newEnt, new ParallaxMovement{
					minVelocity = minVelocity,
					maxVelocity = maxVelocity,
					currentVelocity = UnityEngine.Random.Range(minVelocity, maxVelocity)
				});
		}
	}

	protected override void OnCreate(){
		parallaxParticles = GetEntityQuery(
			typeof(ParallaxMovement),
			typeof(Translation));
	}

	protected override JobHandle OnUpdate(JobHandle deps){
		Random rnd = new Random();
		rnd.InitState((uint)(UnityEngine.Random.value * uint.MaxValue));
		return new ParallaxJob{
				topBound = topBound,
				bottomBound = bottomBound,
				leftBound = leftBound,
				rightBound = rightBound,
				dt = Time.deltaTime,
				rnd = rnd
			}.Schedule(parallaxParticles, deps);
	}

	struct ParallaxJob : IJobForEach<Translation, ParallaxMovement>{
		public float topBound, bottomBound;
		public float leftBound, rightBound;
		public float dt;
		public Random rnd;

		public void Execute(ref Translation pos, ref ParallaxMovement pm){
			// particle is beyond bounds, move to the top and rerandomize veloctiy and x
			if(pos.Value.y < bottomBound){
				pos.Value.y = topBound;
				pos.Value.x = rnd.NextFloat(leftBound, rightBound);
				pm.currentVelocity = rnd.NextFloat(pm.maxVelocity, pm.minVelocity);
			}

			// move the particle
			pos.Value.y -= pm.currentVelocity * dt;
		}
	}
}
