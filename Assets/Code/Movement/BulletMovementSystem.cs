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
	}

    protected override JobHandle OnUpdate(JobHandle handle){

    	SetPhysVelJob velJob = new SetPhysVelJob{
    		playerPositions = playerGroup.ToComponentDataArray<Translation>(Allocator.TempJob)
    	};
    	return velJob.Schedule(physBullets, handle);
    }


	static class MoveUtility{

		public static float3 up(quaternion q){
			return new float3(
				2 * (q.value.x*q.value.y - q.value.w*q.value.z),
				1 - 2 * (q.value.x*q.value.x + q.value.z*q.value.z),
				2 * (q.value.y*q.value.z + q.value.w*q.value.x));
		}
	}

	// only operators on the listed components
	[BurstCompile]
	struct SetPhysVelJob : IJobForEachWithEntity<BulletMovement, PhysicsVelocity, Translation, Rotation>{

		[ReadOnly]
		[DeallocateOnJobCompletion]
		public NativeArray<Translation> playerPositions;

		public void Execute(Entity ent, int idx, [ReadOnly] ref BulletMovement bm, ref PhysicsVelocity vel,
				[ReadOnly] ref Translation pos, [ReadOnly] ref Rotation rotation){

			// the % does nothing for single player, but will distribute the homing 
			//    for multiplayer
			float3 playerPos = (playerPositions.Length > 0) 
				? playerPositions[idx % playerPositions.Length].Value
				: new float3(0, 0, 0);

			float3 forward = MoveUtility.up(rotation.Value);
			float3 bulletToPlayer = math.normalize(playerPos - pos.Value);

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
