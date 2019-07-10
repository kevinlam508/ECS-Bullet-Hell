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

// EXTRA: if the job is slowing down the game, convert the job to work
//    on a per AutoShoot basis rather than a per entity basis
//    requirements:
//        multihashmap of entity - autoshoot pairs to run the job on
//        hashmap of entity - pos/rot to reach from
public class AutoShootSystem : JobComponentSystem{
	public enum ShotPattern { FAN, AROUND }
    public enum AimStyle { Forward, Player }

    // still can't be burst compiled since job adds components using
    //   commandbuffer
    //[BurstCompile]
	struct AutoShootJob : IJobForEachWithEntity<Translation, Rotation>{

        // stores creates to do after job finishes
        public EntityCommandBuffer.Concurrent commandBuffer;
		public float dt;
        public float bulletHeight;

        // buffers of TimePassed for all entities
        // used across multiple jobs, but made it so each system only uses
        //   specific elements from the buffer per entity
        [NativeDisableParallelForRestriction]
        public BufferFromEntity<TimePassed> timePassedBuffers;

        [ReadOnly]
        public BufferFromEntity<AutoShootBuffer> autoShootBuffers;
        [ReadOnly]
        [DeallocateOnJobCompletion]
        public NativeArray<Translation> playerPositions;

        // stats of the bullet to set after creating
        [ReadOnly]
        public NativeList<BulletMovement> bulletMovementStats;
        [ReadOnly]
        public NativeList<BulletDamage> bulletDamageStats;
        public float fixedDT;

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
                        volleyCount.time += 1;
                        timePassed.time -= shoot.period;
                        actualTime = timePassed.time - shoot.startDelay;
                    }

                    // on cooldown, wait for time to pass to reset
                    if(actualTime >= shoot.cooldownDuration && volleyCount.time == shoot.numVolleys){
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
        // timeOffset is how much time to simulate the movement
        private void CreateBullet(int index, Entity bullet, float3 pos,
            quaternion rot, int moveIdx, int damageIdx){

            // buffers commands to do after thread completes
            Entity entity = commandBuffer.Instantiate(index, bullet);
            commandBuffer.SetComponent(index, entity, new Translation {Value = pos});
            commandBuffer.SetComponent(index, entity, new Rotation {Value = rot});
            commandBuffer.SetComponent(index, entity, bulletMovementStats[moveIdx]);
            commandBuffer.SetComponent(index, entity, bulletDamageStats[damageIdx]);
        }

        private void InitBulletPosRot(float3 spawnLocation,
                float3 shooterForward, quaternion fireDirection, 
                float angleOffset, float timeOffset, int moveIdx,
                float3 playerPos, out float3 pos, out quaternion rot){

            // compute position and rotation simulated over timeOffset
            pos = new float3(spawnLocation.x, spawnLocation.y, bulletHeight);
            rot = math.normalize(math.mul(fireDirection,
                quaternion.AxisAngle(shooterForward, angleOffset)));
            BulletMovement moveStats = bulletMovementStats[moveIdx];
            BulletMovementSystem.MoveUtility.SimulateMovement(ref pos, ref rot,
                moveStats, timeOffset, fixedDT, playerPos);
        }

        private void Fire(Entity ent, int index, [ReadOnly] ref Translation position, 
                [ReadOnly] ref Rotation rotation, [ReadOnly] ref AutoShoot shoot,
                float time){
            float interval;
            quaternion fireDirection = quaternion.identity;
            float3 shooterForward = math.forward(rotation.Value);
            float3 spawnPos = position.Value + shoot.sourceOffset;
            float3 playerPos = (playerPositions.Length > 0) 
                ? playerPositions[ent.Index % playerPositions.Length].Value
                : new float3(0, 0, 0);
            switch(shoot.aimStyle){
                case AimStyle.Forward:
                    fireDirection = math.normalize(rotation.Value);
                    break;
                case AimStyle.Player:
                    if(playerPositions.Length > 0){
                        fireDirection = quaternion.LookRotation(
                            shooterForward, 
                            math.normalize(playerPos - spawnPos));
                    }
                    else{ // no player targets, just use rotation
                        fireDirection = math.normalize(rotation.Value);
                    }
                    break;
            }

            switch(shoot.pattern){
                case ShotPattern.FAN:
                    if(shoot.count == 1){
                        InitBulletPosRot(spawnPos, shooterForward, fireDirection, 
                            shoot.centerAngle, time - shoot.period, shoot.moveStatsIdx, 
                            playerPos, out float3 pos, out quaternion rot);
                        CreateBullet(index, shoot.bullet, pos, rot,
                            shoot.moveStatsIdx, shoot.damageStatsIdx);
                    }
                    else{
                        // space between shots is angle / (count - 1) to have 
                        //   shots on edges
                        interval = shoot.angle / (shoot.count - 1);
                        float halfAngle = shoot.angle / 2;
                        for(float rad = -halfAngle; rad <= halfAngle; rad += interval){
                            InitBulletPosRot(spawnPos, shooterForward, fireDirection, 
                                rad + shoot.centerAngle, time - shoot.period, 
                                shoot.moveStatsIdx, playerPos, out float3 pos, out quaternion rot);
                            CreateBullet(index, shoot.bullet, pos, rot,
                                shoot.moveStatsIdx, shoot.damageStatsIdx);
                        }
                    }
                    break;

                case ShotPattern.AROUND:
                    interval = (float)(2 * math.PI / shoot.count);
                    for(float rad = 0.0f; rad < 2 * math.PI; rad += interval){
                        InitBulletPosRot(spawnPos, shooterForward, fireDirection, 
                            rad + shoot.centerAngle, time - shoot.period, 
                            shoot.moveStatsIdx, playerPos, out float3 pos, out quaternion rot);
                        CreateBullet(index, shoot.bullet, pos, rot,
                            shoot.moveStatsIdx, shoot.damageStatsIdx);
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

    protected override void OnDestroy(){
        AutoShootUtility.ClearCaches();
    }

	protected override JobHandle OnUpdate(JobHandle handle){
        EntityCommandBuffer.Concurrent buffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent();

        // init job and run it on entities in the shooters group
        JobHandle jobHandle = new AutoShootJob{
            commandBuffer = buffer,
        	dt = Time.deltaTime,
            bulletHeight = Constants.ENEMY_BULLET_HEIGHT,
            timePassedBuffers = GetBufferFromEntity<TimePassed>(false),
            autoShootBuffers = GetBufferFromEntity<AutoShootBuffer>(true),

            playerPositions = players.ToComponentDataArray<Translation>(Allocator.TempJob),
            bulletMovementStats = AutoShootUtility.movementStatsCache,
            bulletDamageStats = AutoShootUtility.damageStatsCache,
            fixedDT = Time.fixedDeltaTime
        }.Schedule(shooters, handle);

        // tells buffer systems to wait for the job to finish, then
        //   it will perform the commands buffered
        commandBufferSystem.AddJobHandleForProducer(jobHandle);

        return jobHandle;
    }
}
