using UnityEngine;
using Unity.Entities;     // JobComponentSystem
using Unity.Jobs;         // IJob*
using Unity.Transforms;   // Traslation, Rotation
using Unity.Mathematics;  // math
using Unity.Burst;        // BurstCompile
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
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<Translation>()
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
    		util = util,
    		playerPos = EntityManager.GetComponentData<Translation>(player)
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
	[BurstCompile]
	struct SetPhysVelJob : IJobForEach<BulletMovement, PhysicsVelocity, Translation, Rotation>{

		public MoveUtility util;
		public Translation playerPos;

		public void Execute([ReadOnly] ref BulletMovement bm, ref PhysicsVelocity vel,
				[ReadOnly] ref Translation pos, [ReadOnly] ref Rotation rotation){
			float3 forward = util.up(rotation.Value);
			float3 bulletToPlayer = math.normalize(playerPos.Value - pos.Value);

			vel.Linear = math.normalize(forward) * bm.moveSpeed;
			float3 angularVel = new float3(0, 0, 0);
			switch(bm.moveType){
				case MoveType.CURVE:
					angularVel.z = bm.rotateSpeed;
					break;
				case MoveType.HOMING:
					float dot = math.dot(forward, bulletToPlayer);
					if(dot != 0){
						float angle = math.acos(dot);
						float3 cross = math.cross(forward, bulletToPlayer);
						float finalSpeed = math.lerp(
							0,
							bm.rotateSpeed,
							angle / math.PI);
						angularVel.z = math.sign(cross.z) 
							* finalSpeed;
					}
					break;
			}
			vel.Angular = angularVel;
		}
	}
}
