using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;              // Entity, EntityCommandBuffer
using Unity.Jobs;                  // IJob*, JobHandle
using Unity.Transforms;            // Translation, Rotation
using Unity.Mathematics;           // math
using Unity.Burst;                 // BurstCompile
using Unity.Collections;           // ReadOnly

using CustomConstants;             // Constants

/*
 * Notes on AutoShoot parameters
 * Timing:
 *   startDelay: when to start firing
 *   period: time between firing a volleys
 * Volley:
 *   bullet: the object to duplicate
 *   pattern: pattern to duplicate the object
 *   count: number of bullets in a volley
 *   centerAngle: angle from facing to offset pattern center
 *   angle: depends on pattern
 *     FAN: total angle of the fan, ignored if count == 1
 *     AROUND: ignored
 */

[UpdateBefore(typeof(BulletMovementSystem))]
public class AutoShootSystem : JobComponentSystem{
	public enum ShotPattern : int {FAN, AROUND}

    // still can't be burst compiled since job adds components using
    //   commandbuffer
    //[BurstCompile]
	struct AutoShootJob : IJobForEachWithEntity<Translation, Rotation, AutoShoot>{

        // stores creates to do after job finishes
        public EntityCommandBuffer.Concurrent commandBuffer;

        // timing to allow consistent sim
		public float dt;

        // height to put bullets on
        public float bulletHeight;

        // buffer of TimePassed
        [NativeDisableParallelForRestriction]
        public BufferFromEntity<TimePassed> timePassedBuffers;

        // mark args as ReadOnly as much as possible
		public void Execute(Entity ent, int index, [ReadOnly] ref Translation position, 
				[ReadOnly] ref Rotation rotation, [ReadOnly] ref AutoShoot shoot){

            // getting the right TimePassed
            DynamicBuffer<TimePassed> buffer = timePassedBuffers[ent];
            TimePassed timePassed = buffer[shoot.timeIdx];
            timePassed.time += dt;

    		// check and update delay
    		if(shoot.started == 0 && timePassed.time > shoot.startDelay){
    			shoot.started++;
    			timePassed.time -= shoot.startDelay;
    		}

            // fire until below period
            while(shoot.started != 0 && timePassed.time > shoot.period){
                // shoot the pattern
                // buffers commands to do after thread completes
                Fire(ent, index, ref position, ref rotation, ref shoot, ref timePassed);
        		timePassed.time -= shoot.period;
            }

            buffer[shoot.timeIdx] = timePassed;
		}

        // angleOffset is additional rotation clockwise from facing direction
        private void CreateBullet(int index, [ReadOnly] ref Translation position,
                [ReadOnly] ref Rotation rotation, Entity bullet, float angleOffset){
            Entity entity = commandBuffer.Instantiate(index, bullet);
            commandBuffer.SetComponent(index, entity, 
                new Translation {Value = new float3(
                    position.Value.x,
                    position.Value.y,
                    bulletHeight)});
            commandBuffer.SetComponent(index, entity, 
                new Rotation {Value = math.mul(
                    math.normalize(rotation.Value),
                    quaternion.AxisAngle(
                        math.forward(rotation.Value), 
                        angleOffset))
                });
        }

        private void Fire(Entity ent, int index, [ReadOnly] ref Translation position, 
                [ReadOnly] ref Rotation rotation, [ReadOnly] ref AutoShoot shoot,
                ref TimePassed timePassed){
            float interval;
            switch(shoot.pattern){
                case ShotPattern.FAN:
                    if(shoot.count == 1){
                        CreateBullet(index, ref position, ref rotation,
                            shoot.bullet, shoot.centerAngle);
                    }
                    else{
                        // space between shots is angle / (count - 1) to have 
                        //   shots on edges
                        interval = shoot.angle / (shoot.count - 1);
                        float halfAngle = shoot.angle / 2;
                        for(float rad = -halfAngle; rad <= halfAngle; rad += interval){
                            CreateBullet(index, ref position, ref rotation,
                                shoot.bullet, rad + shoot.centerAngle);
                        }
                    }
                    break;

                case ShotPattern.AROUND:
                    interval = (float)(2 * math.PI / shoot.count);
                    for(float rad = 0.0f; rad < 2 * math.PI; rad += interval){
                        CreateBullet(index, ref position, ref rotation,
                            shoot.bullet, rad + shoot.centerAngle);
                    }
                    break;
            }
        }
	}

	private EndSimulationEntityCommandBufferSystem commandBufferSystem;
    private EntityQuery shooters;

    protected override void OnCreateManager(){
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        // shooters will have all entities with Timealive, Translation, 
        //    Rotation, and AutoShoot
        // Translation, Rotation, and AutoShoot will have read only access
        shooters = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    typeof(TimePassed), 
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<AutoShoot>()
                }
            });
    }

	protected override JobHandle OnUpdate(JobHandle handle){

        EntityCommandBuffer.Concurrent buffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent();

        // init job and run it on entities in the shooters group
        JobHandle jobHandle = new AutoShootJob{
            commandBuffer = buffer,
        	dt = Time.deltaTime,
            bulletHeight = Constants.ENEMY_BULLET_HEIGHT,
            timePassedBuffers = GetBufferFromEntity<TimePassed>(false)
        }.Schedule(shooters, handle);

        // tells buffer systems to wait for the job to finish, then
        //   it will perform the commands buffered
        commandBufferSystem.AddJobHandleForProducer(jobHandle);

        return jobHandle;
    }
}
