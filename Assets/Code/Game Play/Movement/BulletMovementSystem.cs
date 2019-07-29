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
[SystemType(ActiveSystemManager.SystemTypes.Stage)]
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


	public static class MoveUtility{

		public static float3 Up(quaternion q){
			return new float3(
				2 * (q.value.x*q.value.y - q.value.w*q.value.z),
				1 - 2 * (q.value.x*q.value.x + q.value.z*q.value.z),
				2 * (q.value.y*q.value.z + q.value.w*q.value.x));
		}

		// returns the rotation speed given the type of movement
		public static float GetRotationSpeed(MoveType type, float rotationSpeedStat, 
				float3 forward, float3 bulletToPlayer){
			float res = 0;
			switch(type){
				case MoveType.CURVE:
					res = rotationSpeedStat;
					break;
				case MoveType.HOMING:
					float dot = math.dot(forward, bulletToPlayer);
					if(dot != 0){
						float angle = math.acos(dot);
						float3 cross = math.cross(forward, bulletToPlayer);
						float finalSpeed = math.lerp(
							0,
							rotationSpeedStat,
							angle / math.PI);
						res = math.sign(cross.z) * finalSpeed;
					}
					break;
			}

			return res;
		}

		public static void SimulateStep(ref float3 pos, ref quaternion rot,
				BulletMovement movementStats, float dt, float3 playerPos){
			float3 forward = Up(rot);
			float3 linMovement = forward * movementStats.moveSpeed * dt;

			// update position after rotation so that rotation speed that
			//   relies on position is unaffected
			Integrator.IntegrateOrientation(ref rot, 
				new float3(0, 0, GetRotationSpeed(movementStats.moveType, 
					movementStats.moveSpeed, forward, math.normalize(playerPos - pos))),
				dt);
			pos += linMovement;
		}

		// simulates moving for time amount of time
		public static void SimulateMovement(ref float3 pos, ref quaternion rot,
				BulletMovement movementStats, float time, float dt,
				float3 playerPos){
			// forward euler using dt sized steps
			while(time > dt){
				SimulateStep(ref pos, ref rot, movementStats, dt, playerPos);
				time -= dt;
			}

			// again with anything remaining
			if(time > 0){
				SimulateStep(ref pos, ref rot, movementStats, time, playerPos);
			}
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
				? playerPositions[ent.Index % playerPositions.Length].Value
				: new float3(0, 0, 0);

			float3 forward = MoveUtility.Up(rotation.Value);
			vel.Linear = forward * bm.moveSpeed;
			vel.Angular = new float3(0, 0,
				MoveUtility.GetRotationSpeed(bm.moveType, bm.rotateSpeed,
					forward, math.normalize(playerPos - pos.Value)));
		}
	}
}
