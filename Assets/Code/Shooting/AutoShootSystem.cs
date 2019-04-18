using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

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

    //[BurstCompile]
	struct AutoShootJob : IJobForEachWithEntity<Translation, Rotation, AutoShoot, TimeAlive>{

        // stores creates to do after job finishes
        public EntityCommandBuffer.Concurrent commandBuffer;

        // timing to allow consistent sim
		public float dt;

        // mark args as ReadOnly as much as possible
		public void Execute(Entity ent, int index, [ReadOnly] ref Translation position, 
				[ReadOnly] ref Rotation rotation, [ReadOnly] ref AutoShoot shoot, 
                ref TimeAlive timeAlive){

            timeAlive.time += dt;
    		// check and update delay
    		if(shoot.started == 0 && timeAlive.time > shoot.startDelay){
    			shoot.started++;
    			timeAlive.time -= shoot.startDelay;
    		}

            // fire until below period
            while(shoot.started != 0 && timeAlive.time > shoot.period){
                // shoot the pattern
                // buffers commands to do after thread completes
                Fire(ent, index, ref position, ref rotation, ref shoot, ref timeAlive);
        		timeAlive.time -= shoot.period;
            }
		}

        private void Fire(Entity ent, int index, [ReadOnly] ref Translation position, 
                [ReadOnly] ref Rotation rotation, [ReadOnly] ref AutoShoot shoot, 
                ref TimeAlive timeAlive){
            Entity entity = Entity.Null;
            float interval;
            switch(shoot.pattern){
                case ShotPattern.FAN:
                    if(shoot.count == 1){
                        entity = commandBuffer.Instantiate(index, shoot.bullet);
                        commandBuffer.SetComponent(index, entity, 
                            new Translation {Value = position.Value});
                        commandBuffer.SetComponent(index, entity, 
                            new Rotation {Value = math.mul(
                                math.normalize(rotation.Value),
                                quaternion.AxisAngle(
                                    math.forward(rotation.Value), 
                                    shoot.centerAngle))
                            });

                        // need to make up for delayed spawn time
                        commandBuffer.AddComponent(index, entity,
                            new LostTime{
                                lostTime = timeAlive.time - shoot.period
                            });
                    }
                    else{
                        // space between shots is angle / (count - 1) to have 
                        //   shots on edges
                        interval = shoot.angle / (shoot.count - 1);
                        float halfAngle = shoot.angle / 2;
                        for(float rad = -halfAngle; rad <= halfAngle; rad += interval){
                            entity = commandBuffer.Instantiate(index, shoot.bullet);
                            commandBuffer.SetComponent(index, entity, 
                                new Translation {Value = position.Value});
                            commandBuffer.SetComponent(index, entity, 
                                new Rotation {Value = math.mul(
                                    math.normalize(rotation.Value),
                                    quaternion.AxisAngle(
                                        math.forward(rotation.Value), 
                                        rad + shoot.centerAngle))
                                });

                            // need to make up for delayed spawn time
                            commandBuffer.AddComponent(index, entity,
                                new LostTime{
                                    lostTime = timeAlive.time - shoot.period
                                });
                        }
                    }
                    break;

                case ShotPattern.AROUND:
                    interval = (float)(2 * math.PI / shoot.count);
                    for(float rad = 0.0f; rad < 2 * math.PI; rad += interval){
                        entity = commandBuffer.Instantiate(index, shoot.bullet);
                        commandBuffer.SetComponent(index, entity, 
                            new Translation {Value = position.Value});
                        commandBuffer.SetComponent(index, entity, 
                            new Rotation {Value = math.mul(
                                math.normalize(rotation.Value),
                                quaternion.AxisAngle(
                                    math.forward(rotation.Value), 
                                    rad + shoot.centerAngle))
                            });

                        // need to make up for delayed spawn time
                        commandBuffer.AddComponent(index, entity,
                            new LostTime{
                                lostTime = timeAlive.time - shoot.period
                            });
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
                All = new ComponentType[]{typeof(TimeAlive), 
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<AutoShoot>()}
            });
    }

	protected override JobHandle OnUpdate(JobHandle handle){

        // init job and run it on entities in the shooters group
        JobHandle jobHandle = new AutoShootJob{
            commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
        	dt = Time.deltaTime
        }.Schedule(shooters, handle);

        // tells buffer systems to wait for the job to finish, then
        //   it will perform the commands buffered
        commandBufferSystem.AddJobHandleForProducer(jobHandle);

        return jobHandle;
    }
}
