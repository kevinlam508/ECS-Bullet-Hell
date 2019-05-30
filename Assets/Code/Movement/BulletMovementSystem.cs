using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

using CustomConstants;

public class BulletMovementSystem : JobComponentSystem{
	public enum MoveType : int { LINEAR, CURVE, HOMING, ENUM_END }

	float accumeT = 0;

	// player tracking
	private EntityQuery playerGroup;
	private Entity player = Entity.Null;

	// entities to operate on
	private EntityQuery bullets;
	private EntityQuery newBullets;

	// singleton to movement functions
	private MoveUtility util;

	protected override void OnCreateManager(){

		// get all entities that have Plyaer and Translation
		playerGroup = GetEntityQuery(
			ComponentType.ReadOnly<Player>(), 
			ComponentType.ReadOnly<Translation>());

		// get all entities with TimeAlive, Translation, Rotation, BulletMovemnt
		//     BulletMovement will have read only access
		// ignore entities cannot have LostTime
		bullets = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                	typeof(TimeAlive), 
                	typeof(Translation),
                	typeof(Rotation),
                    ComponentType.ReadOnly<BulletMovement>()},
                None = new ComponentType[]{
                	typeof(LostTime)
                }
            });

		// like a bullet, but also has LostTime
		newBullets = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                	typeof(TimeAlive), 
                	typeof(Translation),
                	typeof(Rotation),
                    ComponentType.ReadOnly<BulletMovement>(),
                    ComponentType.ReadOnly<LostTime>()
                }
            });

		accumeT = 0f;
		util = new MoveUtility{timeStep = Constants.TIME_STEP};
	}

    protected override JobHandle OnUpdate(JobHandle handle){
    	// get and store the first entity, so NativeArray isn't made every frame
    	if(!EntityManager.Exists(player)){
    		NativeArray<Entity> ents = playerGroup.ToEntityArray(Allocator.TempJob);
    		player = ents[0];
    		ents.Dispose();
    	}

    	// use only multiples of TIME_STEP of time, store any extra
    	accumeT += Time.deltaTime;
    	float useT = 0;
    	while(accumeT > Constants.TIME_STEP){
    		accumeT -= Constants.TIME_STEP;
    		useT += Constants.TIME_STEP;
    	}

    	// setup jobs and schedule them
        MoveBulletsJob normalJob = new MoveBulletsJob{
        	dt = useT,
        	timeStep = Constants.TIME_STEP,
        	playerPos = EntityManager.GetComponentData<Translation>(player),
        	util = util
        };
        MoveNewBulletsJob newJob = new MoveNewBulletsJob{
        	dt = useT,
        	timeStep = Constants.TIME_STEP,
        	playerPos = EntityManager.GetComponentData<Translation>(player),
        	util = util
        };

        // run the jobs on respective groups of entities
        return normalJob.Schedule(bullets, newJob.Schedule(newBullets, handle));
    }


	struct MoveUtility{

		public float timeStep;

		public float3 up(quaternion q){
			return new float3(
				2 * (q.value.x*q.value.y - q.value.w*q.value.z),
				1 - 2 * (q.value.x*q.value.x + q.value.z*q.value.z),
				2 * (q.value.y*q.value.z + q.value.w*q.value.x));
		}

		public void MoveBullet(ref Translation position, ref Rotation rotation, 
				[ReadOnly] ref BulletMovement bullet, ref TimeAlive timeAlive, 
				float dt, [ReadOnly] ref Translation targetPos){

			for(float t = 0; t < dt; t += timeStep){
				switch(bullet.moveType){
					case MoveType.LINEAR:
						Move(ref position, ref rotation, ref bullet, 
							ref timeAlive, timeStep);
						break;
					case MoveType.CURVE:
						Rotate(ref rotation, ref bullet, ref timeAlive, timeStep);
						Move(ref position, ref rotation, ref bullet, 
							ref timeAlive, timeStep);
						break;
					case MoveType.HOMING:
						Move(ref position, ref rotation, ref bullet, 
							ref timeAlive, timeStep);
						TurnTowards(ref position, ref rotation, ref bullet,
							ref timeAlive, timeStep, ref targetPos);
						break;
					default:
						break;
				}
			}

			timeAlive.time += dt;
		}

		// move forward
		public void Move(ref Translation pos, ref Rotation rot, 
				[ReadOnly] ref BulletMovement bullet,
				ref TimeAlive timeAlive, float dt){
			pos.Value = pos.Value + (timeStep * bullet.moveSpeed * 
				up(rot.Value));
		}

		public void Rotate(ref Rotation rot, [ReadOnly] ref BulletMovement bullet, 
				ref TimeAlive timeAlive, float dt){
			rot.Value = math.mul(
				math.normalize(rot.Value), // original rotation
				quaternion.AxisAngle(math.forward(rot.Value), 
					math.radians(bullet.rotateSpeed) * timeStep)); // angle
		}

		// turn up towards player
		public void TurnTowards(ref Translation pos, ref Rotation rot, 
				[ReadOnly] ref BulletMovement bullet, ref TimeAlive timeAlive,
				float dt, ref Translation target){
			float3 goal = math.normalize(target.Value - pos.Value);
			float3 current = math.normalize(up(rot.Value));
			rot.Value = math.slerp(
				rot.Value,
				quaternion.LookRotation(
					math.forward(rot.Value),
					goal - current),
				math.radians(math.abs(bullet.rotateSpeed)) * dt);
		}
	}

	// operates on entities with the 4 listed components
	// uses up as forward movement direction since this is in 2D space
	[BurstCompile]
	struct MoveBulletsJob : IJobForEach<Translation, Rotation, BulletMovement, TimeAlive>{

		// timing to allow consistent sim
		public float dt, timeStep;

		// position of player to home onto
		[ReadOnly] public Translation playerPos;

		// movement utility
		[ReadOnly] public MoveUtility util;

		public void Execute(ref Translation position, ref Rotation rotation, 
				[ReadOnly] ref BulletMovement bullet, ref TimeAlive timeAlive){
			util.MoveBullet(ref position, ref rotation, ref bullet, ref timeAlive, 
				dt, ref playerPos);
		}
	}


	// job to process time lost by a bullet due to spawn delay
	struct MoveNewBulletsJob : IJobForEach<Translation, Rotation, BulletMovement, TimeAlive, LostTime>{

		// timing to allow consistent sim
		public float dt, timeStep;

		// position of player to home onto
		[ReadOnly] public Translation playerPos;

		// movement utility
		[ReadOnly] public MoveUtility util;

		public void Execute(ref Translation position, 
				ref Rotation rotation, [ReadOnly] ref BulletMovement bullet, 
				ref TimeAlive timeAlive, [ReadOnly] ref LostTime lostTime){
			util.MoveBullet(ref position, ref rotation, ref bullet, ref timeAlive, 
				dt + lostTime.lostTime, ref playerPos);
		}
	}
}
