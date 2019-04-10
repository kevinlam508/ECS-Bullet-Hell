using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

using CustomConstants;


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
public class AutoShootSystem : JobComponentSystem
{
	public enum ShotPattern : int {FAN, AROUND}

    //[BurstCompile]
	struct AutoShootJob : IJobForEachWithEntity<Translation, Rotation, AutoShoot, TimeAlive>{

        // stores creates to do after thread finishes
        public EntityCommandBuffer commandBuffer;

        // timing to allow consistent sim
		public float dt, timeStep;

        // mark args as ReadOnly as much as possible
		public void Execute(Entity ent, int index, [ReadOnly] ref Translation position, 
				[ReadOnly] ref Rotation rotation, [ReadOnly] ref AutoShoot shoot, 
                ref TimeAlive timeAlive){

            for(float t = 0; t < dt; t += timeStep){
                timeAlive.time += timeStep;
        		// check and update delay
        		if(shoot.started == 0 && timeAlive.time > shoot.startDelay){
        			shoot.started++;
        			timeAlive.time = shoot.period + 1;
        		}

        		// if not started or not at period yet, skip
        		if(shoot.started == 0 || timeAlive.time < shoot.period){
        			continue;
        		}

                // shoot the pattern
                // buffers commands to do after thread completes
                Entity entity;
                float interval;
        		switch(shoot.pattern){
        			case ShotPattern.FAN:
                        if(shoot.count == 1){
                            entity = commandBuffer.Instantiate(shoot.bullet);
                            commandBuffer.SetComponent(entity, 
                                new Translation {Value = position.Value});
                            commandBuffer.SetComponent(entity, 
                                new Rotation {Value = math.mul(
                                    math.normalize(rotation.Value),
                                    quaternion.AxisAngle(
                                        math.forward(rotation.Value), 
                                        shoot.centerAngle))
                                });
                        }
                        else{
                            // space between shots is angle / (count - 1) to have 
                            //   shots on edges
                            interval = shoot.angle / (shoot.count - 1);
                            float halfAngle = shoot.angle / 2;
                            for(float rad = -halfAngle; rad <= halfAngle; rad += interval){
                                entity = commandBuffer.Instantiate(shoot.bullet);
                                commandBuffer.SetComponent(entity, 
                                    new Translation {Value = position.Value});
                                commandBuffer.SetComponent(entity, 
                                    new Rotation {Value = math.mul(
                                        math.normalize(rotation.Value),
                                        quaternion.AxisAngle(
                                            math.forward(rotation.Value), 
                                            rad + shoot.centerAngle))
                                    });
                            }
                        }
        				break;

                    case ShotPattern.AROUND:
                        interval = (float)(2 * math.PI / shoot.count);
                        for(float rad = 0.0f; rad < 2 * math.PI; rad += interval){
                            entity = commandBuffer.Instantiate(shoot.bullet);
                            commandBuffer.SetComponent(entity, 
                                new Translation {Value = position.Value});
                            commandBuffer.SetComponent(entity, 
                                new Rotation {Value = math.mul(
                                    math.normalize(rotation.Value),
                                    quaternion.AxisAngle(
                                        math.forward(rotation.Value), 
                                        rad + shoot.centerAngle))
                                });
                        }
                        break;
        		}
        		timeAlive.time = 0;
            }
		}
	}

	EndSimulationEntityCommandBufferSystem commandBufferSystem;
    private float accumeT = 0;

    protected override void OnCreateManager()
    {
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

	protected override JobHandle OnUpdate(JobHandle handle)
    {
        accumeT += Time.deltaTime;
        float useT = 0;
        while(accumeT > Constants.TIME_STEP){
            accumeT -= Constants.TIME_STEP;
            useT += Constants.TIME_STEP;
        }

        // init job
        JobHandle job = new AutoShootJob{
            commandBuffer = commandBufferSystem.CreateCommandBuffer(),
        	dt = useT,
            timeStep = Constants.TIME_STEP
        }.ScheduleSingle(this, handle);

        // tells buffer systems to wait for the job to finish, then
        //   it will perform the commands buffered
        commandBufferSystem.AddJobHandleForProducer(job);

        return job;
    }
}
