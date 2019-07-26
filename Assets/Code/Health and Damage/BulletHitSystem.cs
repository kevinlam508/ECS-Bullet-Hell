using UnityEngine;
using Unity.Entities;
using Unity.Physics;             // most physics things
using Unity.Physics.LowLevel;    // CollisionEvents
using Unity.Physics.Systems;     // physics systems
using Unity.Jobs;                // job interfaces, JobComponentSystems
using Unity.Transforms;          // Traslation, Rotation
using Unity.Collections;         // NativeList, NativeMultiHashMap
using Unity.Mathematics;         // math
using Unity.Burst;

using System.Collections;
using System.Collections.Generic;   // Dictionary
using System;                       // Enum

[UpdateAfter(typeof(BulletMovementSystem))]
[UpdateAfter(typeof(PlayerMovementSystem))]
[UpdateAfter(typeof(BuildPhysicsWorld))]
[UpdateBefore(typeof(StepPhysicsWorld))]
public class BulletHitSystem : JobComponentSystem
{
    private enum ObjectType { Player, Enemy, PlayerBullet, EnemyBullet }

    // abstracted incase more data needed later
    struct CollisionInfo{
        public Entity otherEnt;
        public float3 contactPos;
    }

    [BurstCompile]
    struct CollectHitsJob : IContactsJob{

        // the physics world
        [ReadOnly] public CollisionWorld world;

        // map of entities to hits for process hit job
        [WriteOnly] public NativeMultiHashMap<Entity, CollisionInfo>.Concurrent collisions;

        public uint targetMask;
        public uint bulletMask;

        public unsafe void Execute(ref ModifiableContactHeader contactHeader, 
                ref ModifiableContactPoint contactPoint){

            // only store the first contact point of any collision
            //   don't need any others since we only need simple collisions
            if(contactPoint.Index == 0){

                int idxA = contactHeader.BodyIndexPair.BodyAIndex;
                int idxB = contactHeader.BodyIndexPair.BodyBIndex;
                RigidBody rbA = world.Bodies[idxA];
                RigidBody rbB = world.Bodies[idxB];

                int targetIdx = -1, bulletIdx = -1;
                if((rbA.Collider->Filter.BelongsTo & targetMask) != 0
                        && (rbB.Collider->Filter.BelongsTo & bulletMask) != 0){
                    targetIdx = idxA;
                    bulletIdx = idxB;

                }
                else if((rbB.Collider->Filter.BelongsTo & targetMask) != 0
                        && (rbA.Collider->Filter.BelongsTo & bulletMask) != 0){
                    targetIdx = idxB;
                    bulletIdx = idxA;
                }

                if(targetIdx >= 0 && bulletIdx >= 0){
                    collisions.Add(world.Bodies[targetIdx].Entity, 
                        new CollisionInfo{
                            otherEnt = world.Bodies[bulletIdx].Entity,
                            contactPos = contactPoint.Position
                        });
                }
            }
        }
    }

    //[BurstCompile]
    struct ProcessPlayerToBulletHits : IJobForEachWithEntity<Health, Translation>{

        [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;
        public EffectRequestSystem.RequestUtility effectReqUtil;

        [ReadOnly] public NativeMultiHashMap<Entity, CollisionInfo> collisions;
        [ReadOnly] public NativeHashMap<Entity, BulletDamage> damageMap;

        public void Execute(Entity ent, int idx, ref Health health, [ReadOnly] ref Translation pos){

            // run through all the collisions for this entity
            int totalDamage = 0;
            NativeMultiHashMapIterator<Entity> iter;
            CollisionInfo info;
            if(collisions.TryGetFirstValue(ent, out info, out iter)){
                do{

                    BulletDamage damageInfo = damageMap[info.otherEnt];
                    totalDamage += damageInfo.damage;
                    effectReqUtil.CreateParticleRequest(info.contactPos, 
                        EffectRequestSystem.ParticleType.HitSpark);

                    // handle events to happen when bullet hits
                    if(damageInfo.pierceCount > 0){

                        // causes a pierce to count once per frame
                        --damageInfo.pierceCount;
                        commandBuffer.SetComponent(idx, info.otherEnt, damageInfo);
                    }
                    else{
                        commandBuffer.DestroyEntity(idx, info.otherEnt);
                    }
                }while(collisions.TryGetNextValue(out info, ref iter));

                // take damage and die
                health.health -= totalDamage;
                if(health.health <= 0){
                    commandBuffer.DestroyEntity(idx, ent);
                    effectReqUtil.CreateParticleRequest(pos.Value, 
                        EffectRequestSystem.ParticleType.Explosion);
                }
            }
        }

    }

    struct ProcessEnemyToBulletHits : IJobForEachWithEntity<Health, Translation>{

        [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;
        public EffectRequestSystem.RequestUtility effectReqUtil;

        [ReadOnly] public NativeMultiHashMap<Entity, CollisionInfo> collisions;
        [ReadOnly] public NativeHashMap<Entity, BulletDamage> damageMap;

        public void Execute(Entity ent, int idx, ref Health health, [ReadOnly] ref Translation pos){

            // run through all the collisions for this entity
            int totalDamage = 0;
            NativeMultiHashMapIterator<Entity> iter;
            CollisionInfo info;
            if(collisions.TryGetFirstValue(ent, out info, out iter)){
                do{

                    BulletDamage damageInfo = damageMap[info.otherEnt];
                    totalDamage += damageInfo.damage;
                    effectReqUtil.CreateParticleRequest(info.contactPos, 
                        EffectRequestSystem.ParticleType.HitSpark);

                    // handle events to happen when bullet hits
                    if(damageInfo.pierceCount > 0){

                        // causes a pierce to count once per frame
                        --damageInfo.pierceCount;
                        commandBuffer.SetComponent(idx, info.otherEnt, damageInfo);
                    }
                    else{
                        commandBuffer.DestroyEntity(idx, info.otherEnt);
                    }
                }while(collisions.TryGetNextValue(out info, ref iter));

                // take damage and die
                health.health -= totalDamage;
                if(health.health <= 0){
                    commandBuffer.DestroyEntity(idx, ent);
                    effectReqUtil.CreateParticleRequest(pos.Value, 
                        EffectRequestSystem.ParticleType.Explosion);
                }
            }
        }
    }

    struct CopyBulletDamageJob : IJobForEachWithEntity<BulletDamage>{
        [WriteOnly] public NativeHashMap<Entity, BulletDamage>.Concurrent damageMap;

        public void Execute(Entity ent, int idx, [ReadOnly] ref BulletDamage damage){
            damageMap.TryAdd(ent, damage);
        }
    }

    // other systems
    BeginInitializationEntityCommandBufferSystem commandBufferSystem;
    EffectRequestSystem effectSystem;

    // phys systems set callbacks up with
    BuildPhysicsWorld buildPhysWorld;
    StepPhysicsWorld stepPhysWorld;

    // entity groups
    EntityQuery[] groups;

    // collections of collision data
    Dictionary<ObjectType, NativeMultiHashMap<Entity, CollisionInfo>> collisions 
        = new Dictionary<ObjectType, NativeMultiHashMap<Entity, CollisionInfo>>();
    Dictionary<ObjectType, NativeHashMap<Entity, BulletDamage>> damageMaps
        = new Dictionary<ObjectType, NativeHashMap<Entity, BulletDamage>>();

    // physics layer masks
    uint[] masks;

    JobHandle job;

    protected override void OnCreateManager()
    {
        // get other systems
        commandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        effectSystem = World.GetOrCreateSystem<EffectRequestSystem>();
        buildPhysWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        stepPhysWorld = World.GetOrCreateSystem<StepPhysicsWorld>();

        int numTypes = Enum.GetValues(typeof(ObjectType)).Length;

        groups = new EntityQuery[numTypes];
        groups[(int)ObjectType.Player] = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<Player>(),
                    typeof(Health),
                    ComponentType.ReadOnly<Translation>()
                }
            });
        groups[(int)ObjectType.Enemy] = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<Enemy>(),
                    typeof(Health),
                    ComponentType.ReadOnly<Translation>()
                }
            });
        groups[(int)ObjectType.PlayerBullet] = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<PlayerBullet>(),
                    ComponentType.ReadOnly<BulletDamage>()
                }
            });
        groups[(int)ObjectType.EnemyBullet] = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<EnemyBullet>(),
                    ComponentType.ReadOnly<BulletDamage>()
                }
            });

        // TODO: update with more flexible way of looking up physics layers
        masks = new uint[numTypes];
        masks[(int)ObjectType.Player] = 1 << 3;
        masks[(int)ObjectType.Enemy] = 1 << 1;
        masks[(int)ObjectType.PlayerBullet] = 1 << 2;
        masks[(int)ObjectType.EnemyBullet] = 1 << 0;

        // init slots in the dictionaries
        collisions.Add(ObjectType.Player, new NativeMultiHashMap<Entity, CollisionInfo>());
        collisions.Add(ObjectType.Enemy, new NativeMultiHashMap<Entity, CollisionInfo>());
        damageMaps.Add(ObjectType.PlayerBullet, new NativeHashMap<Entity, BulletDamage>());
        damageMaps.Add(ObjectType.EnemyBullet, new NativeHashMap<Entity, BulletDamage>());
    }

    private void DisposeContainers(){

        foreach(KeyValuePair<ObjectType, NativeMultiHashMap<Entity, CollisionInfo>> pair in collisions){
            if(pair.Value.IsCreated){
                pair.Value.Dispose();
            }
        }

        foreach(KeyValuePair<ObjectType, NativeHashMap<Entity, BulletDamage>> pair in damageMaps){
            if(pair.Value.IsCreated){
                pair.Value.Dispose();
            }
        }
    }

    protected override void OnStopRunning(){
        // ensure job is done before disposing containers
        job.Complete();
        DisposeContainers();
    }

    protected override JobHandle OnUpdate(JobHandle handle){

        // delegate for physics to add my jobs to its pipeline
        SimulationCallbacks.Callback callback = (ref ISimulation sim, 
            ref PhysicsWorld world, JobHandle deps) =>
        {
            // delete containers from previous frame
            DisposeContainers();

            JobHandle playerToBulletCollect = InitCollectBulletDataJob(ref sim, 
                ref world, deps, ObjectType.Player, ObjectType.EnemyBullet);
            JobHandle enemyToBulletCollect = InitCollectBulletDataJob(ref sim, 
                ref world, deps, ObjectType.Enemy, ObjectType.PlayerBullet);

            JobHandle processJob = InitProcessJobs(playerToBulletCollect, enemyToBulletCollect);
            job = processJob;
            return processJob;
        };

        stepPhysWorld.EnqueueCallback(SimulationCallbacks.Phase.PostCreateContacts, 
            callback);

        return handle;
    }

    // collects data related to player collisions
    private JobHandle InitCollectBulletDataJob(ref ISimulation sim, ref PhysicsWorld world, 
            JobHandle deps, ObjectType target, ObjectType bullet){

        // first arg is max capacity, setting as max possible collision pairs
        collisions[target] = new NativeMultiHashMap<Entity, CollisionInfo>(
            groups[(int)bullet].CalculateLength() * groups[(int)target].CalculateLength(), 
            Allocator.TempJob);
        damageMaps[bullet] = new NativeHashMap<Entity, BulletDamage>(
            groups[(int)bullet].CalculateLength(),
            Allocator.TempJob);

        JobHandle copyDamageJob = new CopyBulletDamageJob{
            damageMap = damageMaps[bullet].ToConcurrent()
        }.Schedule(groups[(int)bullet], deps);

        JobHandle collectJob = new CollectHitsJob{
            world = buildPhysWorld.PhysicsWorld.CollisionWorld,
            collisions = collisions[target].ToConcurrent(),
            targetMask = masks[(int)target],
            bulletMask = masks[(int)bullet]
        }.Schedule(sim, ref world, deps);

        return JobHandle.CombineDependencies(copyDamageJob, collectJob);
    }

    // need to schedule these in order because of overlapping component types
    private JobHandle InitProcessJobs(JobHandle playerDeps, JobHandle enemyDeps){

        JobHandle playerProcessJob = new ProcessPlayerToBulletHits{
            commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            effectReqUtil = effectSystem.GetUtility(),
            collisions = collisions[ObjectType.Player],
            damageMap = damageMaps[ObjectType.EnemyBullet]
        }.Schedule(groups[(int)ObjectType.Player], playerDeps);

        JobHandle enemyProcessJob = new ProcessEnemyToBulletHits{
            commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            effectReqUtil = effectSystem.GetUtility(),
            collisions = collisions[ObjectType.Enemy],
            damageMap = damageMaps[ObjectType.PlayerBullet]
        }.Schedule(groups[(int)ObjectType.Enemy], 
            JobHandle.CombineDependencies(enemyDeps, playerProcessJob));

        commandBufferSystem.AddJobHandleForProducer(enemyProcessJob);
        effectSystem.AddDependency(enemyProcessJob);
        return enemyProcessJob;
    }
}