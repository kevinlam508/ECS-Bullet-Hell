using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

using CustomConstants;

public class BulletMovementSystem : JobComponentSystem
{
	public enum MoveType : int { LINEAR, QUADRATIC, CURVE, HOMING, ENUM_END }
	public enum InitState : int { NOT_INIT, INIT }

	Unity.Mathematics.Random rnd;
	float accumeT = 0;

	// operates on all entities with the 4 listed components
	// uses up as forward movement direction since this is in 2D space
	[BurstCompile]
	struct MoveBulletsJob : IJobForEach<Translation, Rotation, BulletMovement, TimeAlive>{

		// timing to allow consistent sim
		public float dt, timeStep;

		// position of player to home onto
		[ReadOnly] public Translation playerPos;
		public Unity.Mathematics.Random rnd;

		public void Execute(ref Translation position, ref Rotation rotation, 
				[ReadOnly] ref BulletMovement bullet, ref TimeAlive timeAlive){
			if(bullet.init == InitState.NOT_INIT){
				bullet.initPos = position.Value;
				bullet.initRot = rotation.Value;
				bullet.init = InitState.INIT;
			}
			MoveBullet(ref position, ref rotation, ref bullet, ref timeAlive, 
				dt);
		}

		float3 up(quaternion q){
			return new float3(
				2 * (q.value.x*q.value.y - q.value.w*q.value.z),
				1 - 2 * (q.value.x*q.value.x + q.value.z*q.value.z),
				2 * (q.value.y*q.value.z + q.value.w*q.value.x));
		}

		void MoveBullet(ref Translation position, ref Rotation rotation, 
				[ReadOnly] ref BulletMovement bullet, ref TimeAlive timeAlive, 
				float dt){

			for(float t = 0; t < dt; t += timeStep){
				switch(bullet.moveType){
					case MoveType.LINEAR:
						Move(ref position, ref rotation, ref bullet, 
							ref timeAlive, timeStep);
						break;
					case MoveType.QUADRATIC:
						float a = 1f;
						float b = 1f;
						rotation.Value = math.mul(bullet.initRot,
							quaternion.AxisAngle(math.forward(rotation.Value), 
								math.atan(a*2*timeAlive.time + b)));

						position.Value = position.Value 
							+ up(rotation.Value)*(a*2*timeAlive.time
								+ b) * timeStep;

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
							ref timeAlive, timeStep, ref playerPos);
						break;
					default:
						break;
				}
			}

			timeAlive.time += dt;
			//bullet.moveType = (MoveType)(rnd.NextUInt() % (int)MoveType.ENUM_END);
		}

		// move forward
		void Move(ref Translation pos, ref Rotation rot, 
				[ReadOnly] ref BulletMovement bullet,
				ref TimeAlive timeAlive, float dt){
			pos.Value = pos.Value + (timeStep * bullet.moveSpeed * 
				up(rot.Value));
		}

		void Rotate(ref Rotation rot, [ReadOnly] ref BulletMovement bullet, 
				ref TimeAlive timeAlive, float dt){
			rot.Value = math.mul(
				math.normalize(rot.Value), // original rotation
				quaternion.AxisAngle(math.forward(rot.Value), 
					math.radians(bullet.rotateSpeed) * timeStep)); // angle
		}

		// turn up towards player
		void TurnTowards(ref Translation pos, ref Rotation rot, 
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

	private EntityQuery playerGroup;
	private Entity player = Entity.Null;

	protected override void OnCreateManager(){
		rnd = new Unity.Mathematics.Random();
		rnd.InitState((uint)UnityEngine.Random.Range(1, int.MaxValue));

		// get all entities that have Plyaer and Translation
		playerGroup = GetEntityQuery(
			ComponentType.ReadOnly<Player>(), 
			ComponentType.ReadOnly<Translation>());

		accumeT = 0f;
	}

    protected override JobHandle OnUpdate(JobHandle handle)
    {
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

    	// setup job and schedule it
        MoveBulletsJob job = new MoveBulletsJob{
        	dt = useT,
        	timeStep = Constants.TIME_STEP,
        	playerPos = EntityManager.GetComponentData<Translation>(player),
        	rnd = this.rnd
        };

        rnd.InitState(rnd.NextUInt());
        return job.Schedule(this, handle);
    }
}
