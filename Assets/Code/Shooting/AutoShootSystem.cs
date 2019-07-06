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
 *  Aiming
 *   aimStyle:
 *     Forward: based on facing direction
 *     Player:  based on direct to player
 *   angle: depends on pattern
 *     FAN: total angle of the fan, ignored if count == 1
 *     AROUND: ignored
 */

public class AutoShootSystem : JobComponentSystem{
	public enum ShotPattern { FAN, AROUND }
    public enum AimStyle { Forward, Player }

    // still can't be burst compiled since job adds components using
    //   commandbuffer
    //[BurstCompile]
	struct AutoShootJob : IJobForEachWithEntity<Translation, Rotation>{

        // stores creates to do after job finishes
        public EntityCommandBuffer.Concurrent commandBuffer;

        // timing to allow consistent sim
		public float dt;

        // height to put bullets on
        public float bulletHeight;

        // buffers of TimePassed for all entities
        // used across multiple jobs, but made it so each system only uses
        //   specific elements from the buffer per entity
        [NativeDisableParallelForRestriction]
        public BufferFromEntity<TimePassed> timePassedBuffers;

        [ReadOnly]
        public BufferFromEntity<AutoShootBuffer> autoShootBuffers;

        [ReadOnly]
        public NativeArray<Translation> playerPos;

        // mark args as ReadOnly as much as possible
		public void Execute(Entity ent, int index, [ReadOnly] ref Translation position, 
				[ReadOnly] ref Rotation rotation){

            DynamicBuffer<AutoShootBuffer> shootBuffer = autoShootBuffers[ent];
            DynamicBuffer<TimePassed> buffer = timePassedBuffers[ent];
            for(int i = 0; i < shootBuffer.Length; ++i){
                AutoShoot shoot = shootBuffer[i].val;

                // getting the right TimePassed
                TimePassed timePassed = buffer[shoot.timeIdx];
                TimePassed volleyCount = buffer[shoot.volleyCountIdx];
                timePassed.time += dt;

                if(timePassed.time > shoot.startDelay){
                    float actualTime = timePassed.time - shoot.startDelay;

                    // EXTRA: add recovery for bullets not fired exactly when they should
                    // fire if above period and not on cooldown
                    while(actualTime >= shoot.period && volleyCount.time < shoot.numVolleys){
                        // shoot the pattern
                        Fire(ent, index, ref position, ref rotation, ref shoot, actualTime);
                        timePassed.time -= shoot.period;
                        volleyCount.time += 1;
                        actualTime = timePassed.time - shoot.startDelay;
                    }

                    // on cooldown, wait for time to pass to reset
                    if(actualTime >= shoot.cooldownDuration){
                        volleyCount.time = 0;
                        timePassed.time -= shoot.cooldownDuration;
                    }
                }

                // writing updates out
                buffer[shoot.timeIdx] = timePassed;
                buffer[shoot.volleyCountIdx] = volleyCount;
            }
		}

        // angleOffset is additional rotation clockwise from facing direction
        private void CreateBullet(int index, [ReadOnly] ref Translation position,
                float3 shooterForward, Entity bullet,
                quaternion fireDirection, float angleOffset){
            // buffers commands to do after thread completes
            Entity entity = commandBuffer.Instantiate(index, bullet);
            commandBuffer.SetComponent(index, entity, 
                new Translation {Value = new float3(
                    position.Value.x,
                    position.Value.y,
                    bulletHeight)});
            commandBuffer.SetComponent(index, entity, 
                new Rotation {Value = math.mul(
                    fireDirection,
                    quaternion.AxisAngle(shooterForward, angleOffset))
                });
        }

        private void Fire(Entity ent, int index, [ReadOnly] ref Translation position, 
                [ReadOnly] ref Rotation rotation, [ReadOnly] ref AutoShoot shoot,
                float time){
            float interval;
            quaternion fireDirection = quaternion.identity;
            float3 shooterForward = math.forward(rotation.Value);
            switch(shoot.aimStyle){
                case AimStyle.Forward:
                    fireDirection = math.normalize(rotation.Value);
                    break;
                case AimStyle.Player:
                    fireDirection = quaternion.LookRotation(
                        shooterForward, 
                        math.normalize(playerPos[index % playerPos.Length].Value - position.Value));
                    break;
            }

            switch(shoot.pattern){
                case ShotPattern.FAN:
                    if(shoot.count == 1){
                        CreateBullet(index, ref position, shooterForward,
                            shoot.bullet, fireDirection, shoot.centerAngle);
                    }
                    else{
                        // space between shots is angle / (count - 1) to have 
                        //   shots on edges
                        interval = shoot.angle / (shoot.count - 1);
                        float halfAngle = shoot.angle / 2;
                        for(float rad = -halfAngle; rad <= halfAngle; rad += interval){
                            CreateBullet(index, ref position, shooterForward,
                                shoot.bullet, fireDirection, rad + shoot.centerAngle);
                        }
                    }
                    break;

                case ShotPattern.AROUND:
                    interval = (float)(2 * math.PI / shoot.count);
                    for(float rad = 0.0f; rad < 2 * math.PI; rad += interval){
                        CreateBullet(index, ref position, shooterForward,
                            shoot.bullet, fireDirection, rad + shoot.centerAngle);
                    }
                    break;
            }
        }
	}

    // there are multiple CommandBufferSystems and the one used will affect 
    //   when, relative to other systems, the commands are played back
	private BeginInitializationEntityCommandBufferSystem commandBufferSystem;
    private EntityQuery shooters;
    private EntityQuery players;
    private NativeArray<Translation> playerPos;

    protected override void OnCreateManager(){
        commandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();

        // shooters will have all entities with Timealive, Translation, 
        //    Rotation, and AutoShoot
        // Translation, Rotation, and AutoShoot will have read only access
        shooters = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    typeof(TimePassed), 
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<AutoShootBuffer>()
                }
            });
        players = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    typeof(Player), 
                    ComponentType.ReadOnly<Translation>()
                }
            }); 
    }

	protected override JobHandle OnUpdate(JobHandle handle){

        DisposeContainers();
        playerPos = players.ToComponentDataArray<Translation>(Allocator.TempJob);
        EntityCommandBuffer.Concurrent buffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent();

        // init job and run it on entities in the shooters group
        JobHandle jobHandle = new AutoShootJob{
            commandBuffer = buffer,
        	dt = Time.deltaTime,
            bulletHeight = Constants.ENEMY_BULLET_HEIGHT,
            timePassedBuffers = GetBufferFromEntity<TimePassed>(false),
            autoShootBuffers = GetBufferFromEntity<AutoShootBuffer>(true),
            playerPos = playerPos
        }.Schedule(shooters, handle);

        // tells buffer systems to wait for the job to finish, then
        //   it will perform the commands buffered
        commandBufferSystem.AddJobHandleForProducer(jobHandle);

        return jobHandle;
    }

    private void DisposeContainers(){
        if(playerPos.IsCreated){
            playerPos.Dispose();
        }
    }

    protected override void OnStopRunning(){
        DisposeContainers();
    }
}
