using UnityEngine;
using Unity.Entities;     // JobComponentSystem
using Unity.Jobs;         // IJob*
using Unity.Transforms;   // Traslation, Rotation
using Unity.Mathematics;  // math
using Unity.Burst;        // BurstCompole
using Unity.Collections;  // Native*
using Unity.Physics;      // physics things

using CustomConstants;

[UpdateBefore(typeof(TrackerRemovalSystem))]
public class BulletMovementSystem : JobComponentSystem{
	public enum MoveType : int { LINEAR, CURVE, HOMING, ENUM_END }

	// player tracking
	private EntityQuery playerGroup;
	private Entity player = Entity.Null;

	// entities to operate on
	private EntityQuery physBullets;

	// singleton to movement functions
	private MoveUtility util;

	protected override void OnCreateManager(){

		// get all entities that have Plyaer and Translation
		playerGroup = GetEntityQuery(
			ComponentType.ReadOnly<Player>(), 
			ComponentType.ReadOnly<Translation>());

		// get all entities with BulletMovement, PhysicsVelocity, Rotation
		physBullets = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                	typeof(PhysicsVelocity),
                    ComponentType.ReadOnly<BulletMovement>(),
                    ComponentType.ReadOnly<Rotation>()
                }
            });

		util = new MoveUtility();
	}

    protected override JobHandle OnUpdate(JobHandle handle){
    	// get and store the first entity, so NativeArray isn't made every frame
    	if(!EntityManager.Exists(player)){
    		NativeArray<Entity> ents = playerGroup.ToEntityArray(Allocator.TempJob);
    		player = ents[0];
    		ents.Dispose();
    	}

    	SetPhysVelJob velJob = new SetPhysVelJob{
    		util = util
    	};
    	return velJob.Schedule(physBullets, handle);
    }


	struct MoveUtility{

		public float3 up(quaternion q){
			return new float3(
				2 * (q.value.x*q.value.y - q.value.w*q.value.z),
				1 - 2 * (q.value.x*q.value.x + q.value.z*q.value.z),
				2 * (q.value.y*q.value.z + q.value.w*q.value.x));
		}
	}

	// only operators on the listed components
	struct SetPhysVelJob : IJobForEach<BulletMovement, PhysicsVelocity, Rotation>{

		public MoveUtility util;

		public void Execute([ReadOnly] ref BulletMovement bm, ref PhysicsVelocity vel,
				[ReadOnly] ref Rotation rotation){
			float3 up = util.up(rotation.Value);

			vel.Linear = math.normalize(up) * bm.moveSpeed;
			float3 angularVel = new float3(0, 0, 0);
			switch(bm.moveType){
				case MoveType.CURVE:
					angularVel.z = bm.rotateSpeed;
					break;
			}
			vel.Angular = angularVel;
		}
	}
}
