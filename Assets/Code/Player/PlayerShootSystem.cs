using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;              // Entity, EntityCommandBuffer
using Unity.Jobs;                  // IJob*, JobHandle
using Unity.Transforms;            // Translation, Rotation
using Unity.Burst;                 // BurstCompile
using Unity.Collections;           // ReadOnly
using Unity.Mathematics; 		   // math
using Unity.Physics;               // *Collider

using CustomConstants;			   // Constants

using Random = Unity.Mathematics.Random;

public class PlayerShootSystem : JobComponentSystem
{
	private BeginInitializationEntityCommandBufferSystem commandBufferSystem;
    private EntityQuery players;
    private EntityQuery waterBullets;

	protected override void OnCreateManager(){
        commandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();

        // get entities that define players
        players = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<PlayerShoot>()
                }
            });

        waterBullets = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    typeof(Translation),
                    typeof(Scale),
                    ComponentType.ReadOnly<WaterShootIndex>(),
                    typeof(PhysicsCollider),
                    typeof(BulletDamage)
                }
            });
    }

    protected override JobHandle OnUpdate(JobHandle deps){

        PlayerShootJobData data = new PlayerShootJobData{
            commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            dt = Time.deltaTime,
            bulletHeight = Constants.PLAYER_BULLET_HEIGHT,
            timePassedBuffers = GetBufferFromEntity<TimePassed>(false),
            shooting = (Input.GetAxis("Fire1") > 0)
        };

    	JobHandle shootJob = ScheduleWaterWeapon(data, deps);

    	return shootJob;
    }

    protected override void OnStopRunning(){
        if(bubbleData.IsCreated){
            bubbleData.Dispose();
        }
    }

    private struct PlayerShootJobData{
        // buffer for entity changing commands
        public EntityCommandBuffer.Concurrent commandBuffer;

        // timing
        [NativeDisableParallelForRestriction]
        public BufferFromEntity<TimePassed> timePassedBuffers;
        public float dt;

        // making bullets
        public float bulletHeight;
        public bool shooting;

        public Entity CreateBullet(int index, [ReadOnly] ref Translation position,
                [ReadOnly] ref Rotation rotation, Entity bullet, float angle){
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
                        angle))
                });
            return entity;
        }
    } 

    /* 
     *  Basic weapon
     *
     *  Just fires bullets, no special properties
     */

    private JobHandle ScheduleBasicWeapon(PlayerShootJobData jobData, JobHandle deps){
        JobHandle shootJob = new PlayerShootJob{
            data = jobData
        }.Schedule(players, deps);

        // tells buffer systems to wait for the job to finish, then
        //   it will perform the commands buffered
        commandBufferSystem.AddJobHandleForProducer(shootJob);

        return shootJob;
    }

    // basic shooting
    private struct PlayerShootJob : IJobForEachWithEntity<PlayerShoot, Translation, Rotation>{

        public PlayerShootJobData data;

    	public void Execute(Entity ent, int index, [ReadOnly] ref PlayerShoot shoot, 
    			[ReadOnly] ref Translation pos, [ReadOnly] ref Rotation rotation){

    		// getting the right TimePassed
            DynamicBuffer<TimePassed> buffer = data.timePassedBuffers[ent];
            TimePassed timePassed = buffer[shoot.timeIdx];
			timePassed.time += data.dt;

            // fire until below period
            if(data.shooting && timePassed.time > shoot.shotCooldown){
                // shoot the pattern
                // buffers commands to do after thread completes
                data.CreateBullet(index, ref pos, ref rotation, shoot.bullet, 0);
        		timePassed.time = 0;
            }

            buffer[shoot.timeIdx] = timePassed;
    	}
    }

    /*
     *  Water shooting weapon
     *
     *  Player can charge the bullet and it stays near the player
     *  When released, bullets spray in every direction, amount depends on charge.
     *  If hit before release, deals damage depending on charge.
     */

    private NativeArray<WaterShootData> bubbleData;
    private float bubbleBurstTopSpeed = 6f;
    private float maxChargeTime = 8f;

    private JobHandle ScheduleWaterWeapon(PlayerShootJobData jobData, JobHandle deps){    

        if(!bubbleData.IsCreated){
            bubbleData = new NativeArray<WaterShootData>(
                players.CalculateLength(),
                Allocator.Persistent);
        }

        JobHandle job = new WaterShootJob{
                data = jobData,
                bubbleData = bubbleData,
                maxChargeTime = maxChargeTime,
                bubbleBurstTopSpeed = bubbleBurstTopSpeed
            }.Schedule(players, deps);

        Random rnd = new Random();
        rnd.InitState((uint)(UnityEngine.Random.value * uint.MaxValue));
        job = new WaterShootUpdateBulletJob{
                commandBuffer = jobData.commandBuffer,
                bubbleData = bubbleData,
                rnd = rnd
            }.Schedule(waterBullets, job);

        job = new WaterShootPostUpdateJob{
                data = jobData,
                bubbleData = bubbleData
            }.Schedule(players, job);

        commandBufferSystem.AddJobHandleForProducer(job);
        return job;
    }

    private enum WaterShootState{ 
        NotFired,         // nothing shot
        JustFired,        // just shot this frame, bullet doesn't actually exsist yet
        Burst,            // stopped shooting, so create burst
        UpdatedBurst,     // updated bullet for burst
        Charging,         // still shooting
        UpdatedCharging   // updated bullet for charging
    }

    private struct WaterShootData{
        public float timeHeld;
        public float3 position;
        public WaterShootState state; 

        // inital sizes for scaling
        public float initialScale;
        public float initialColliderRadius;

        // bullet for burst
        public Entity bullet;
        public float topSpeed;
    }

    private struct WaterShootIndex : IComponentData{
        public int index;
    }

    // determines what action to take
    private struct WaterShootJob : IJobForEachWithEntity<PlayerShoot, Translation, Rotation>{

        public PlayerShootJobData data;
        public NativeArray<WaterShootData> bubbleData;
        public float maxChargeTime;
        public float bubbleBurstTopSpeed;

        public void Execute(Entity ent, int index, [ReadOnly] ref PlayerShoot shoot, 
                [ReadOnly] ref Translation pos, [ReadOnly] ref Rotation rotation){

            // getting the right TimePassed
            DynamicBuffer<TimePassed> buffer = data.timePassedBuffers[ent];
            TimePassed timePassed = buffer[shoot.timeIdx];
            timePassed.time += data.dt;

            if(timePassed.time >= shoot.shotCooldown){

                // get data to update the bubble with
                WaterShootData shootData = bubbleData[index];
                shootData.timeHeld = math.min(timePassed.time - shoot.shotCooldown, maxChargeTime);
                shootData.position = pos.Value;
                shootData.initialScale = shoot.initialScale;
                shootData.initialColliderRadius = shoot.initialColliderRadius;
                shootData.bullet = shoot.bullet;
                shootData.topSpeed = bubbleBurstTopSpeed;

                if(data.shooting){
                    // create a new bubble
                    if(shootData.state == WaterShootState.NotFired){
                        Entity bullet = data.CreateBullet(index, ref pos, ref rotation, shoot.bullet, 0);
                        data.commandBuffer.AddComponent(index, bullet, 
                            new WaterShootIndex{ index = index });
                        data.commandBuffer.AddComponent(index, bullet, 
                            new Scale{ Value = .3f });
                        shootData.state = WaterShootState.JustFired;
                    }
                    else{
                        shootData.state = WaterShootState.Charging;
                    }
                }
                else{
                    timePassed.time = shoot.shotCooldown;

                    // bullet was updated last frame, so it exists and was charged,
                    // so create burst of bullets
                    if(shootData.state == WaterShootState.UpdatedCharging){
                        shootData.state = WaterShootState.Burst;
                    }
                }

                bubbleData[index] = shootData;
            }

            buffer[shoot.timeIdx] = timePassed;

        }
    }

    // TODO: make scaleFactor update collider's radius, get a better way of getting bullet radius

    // updates the related bullet if it exists
    private struct WaterShootUpdateBulletJob : IJobForEachWithEntity<Translation, Scale, WaterShootIndex, PhysicsCollider, BulletDamage>{

        public EntityCommandBuffer.Concurrent commandBuffer;
        public NativeArray<WaterShootData> bubbleData;
        public Random rnd;

        public void Execute(Entity ent, int index, ref Translation pos, ref Scale scale,
                    [ReadOnly] ref WaterShootIndex waterIndex, ref PhysicsCollider collider,
                    ref BulletDamage damage){
            WaterShootData data = bubbleData[waterIndex.index];

            // stick to player
            pos.Value = new float3(data.position.x, data.position.y + scale.Value,
                Constants.PLAYER_BULLET_HEIGHT);

            // increase damage and size 
            float progress = math.sqrt(data.timeHeld);
            float scaleFactor = 1 + progress;
            scale.Value = data.initialScale * scaleFactor;
            unsafe{
                if(collider.ColliderPtr->Type == ColliderType.Cylinder){
                    CylinderCollider* col = (CylinderCollider*) collider.ColliderPtr;
                    col->Radius = data.initialColliderRadius * scale.Value;
                }
            }
            damage.damage = (int)(scaleFactor * math.pow(scaleFactor, .25)); // temporary

            if(data.state == WaterShootState.Burst){
                commandBuffer.DestroyEntity(index, ent);

                for(float angle = 0; angle < 2 * math.PI; angle += rnd.NextFloat(1 / (progress + 1),  1 / progress)){
                    CreateBurstLine(data.bullet, index, angle, pos.Value,
                        data.topSpeed, (int)(progress * 2));
                }
            }
            else if(data.state == WaterShootState.NotFired){
                commandBuffer.DestroyEntity(index, ent);
            }

            // notify shooter that bullet existed and was updated
            ++data.state;

            bubbleData[waterIndex.index] = data;
        }

        public void CreateBurstLine(Entity bullet, int index, float angle, float3 position, float speed,
                int bulletsInLine){
            for(int i = 0; i < bulletsInLine; ++i){
                float speedScale = .5f + (.5f / bulletsInLine * i);
                Entity entity = commandBuffer.Instantiate(index, bullet);
                commandBuffer.SetComponent(index, entity, 
                    new Translation {Value = position});
                commandBuffer.SetComponent(index, entity, 
                    new Rotation {Value = 
                        quaternion.AxisAngle(
                            new float3(0, 0, 1), 
                            angle)
                    });
                commandBuffer.SetComponent(index, entity, 
                    new BulletMovement {
                        moveType = BulletMovementSystem.MoveType.LINEAR,
                        moveSpeed = speed * speedScale,
                        rotateSpeed = 0
                    });
            }
        }
    }

    // reports to shooter what happened
    private struct WaterShootPostUpdateJob : IJobForEachWithEntity<PlayerShoot>{

        public PlayerShootJobData data;
        public NativeArray<WaterShootData> bubbleData;

        public void Execute(Entity ent, int index, [ReadOnly] ref PlayerShoot shoot){
            WaterShootData waterData = bubbleData[index];

            // failed to update or bullet will not exist anymore, reset
            if(waterData.state != WaterShootState.UpdatedCharging && waterData.state != WaterShootState.NotFired
                    && waterData.state != WaterShootState.JustFired){
                waterData.state = WaterShootState.NotFired;

                DynamicBuffer<TimePassed> buffer = data.timePassedBuffers[ent];
                TimePassed timePassed = buffer[shoot.timeIdx];
                timePassed.time = 0;
                buffer[shoot.timeIdx] = timePassed;
                bubbleData[index] = waterData;
            }

        }
    }
}
