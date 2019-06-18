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

[UpdateAfter(typeof(BulletMovementSystem))]
[UpdateAfter(typeof(PlayerMovementSystem))]
[UpdateAfter(typeof(BuildPhysicsWorld))]
[UpdateBefore(typeof(StepPhysicsWorld))]
public class BulletHitSystem : JobComponentSystem
{
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
    struct ProcessPlayerToBulletHits : IJobNativeMultiHashMapVisitKeyValue<Entity, CollisionInfo>{

        // store commands to be processed outside of jobs
        [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;

        // the physics world
        [ReadOnly] public CollisionWorld world;
        public ParticleRequestSystem.ParticleRequestUtility partUtil;

        public void ExecuteNext(Entity player, CollisionInfo data){

            // delete other entity for now
            Entity other = data.otherEnt;
            commandBuffer.DestroyEntity(other.Index, other);
            partUtil.CreateRequest(other.Index, commandBuffer,
                data.contactPos, ParticleRequestSystem.ParticleType.HitSpark);
        }

    }

    struct ProcessEnemyToBulletHits : IJobForEachWithEntity<Health, Translation>{

        [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;
        public ParticleRequestSystem.ParticleRequestUtility partUtil;

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
                    partUtil.CreateRequest(idx, commandBuffer, info.contactPos, 
                        ParticleRequestSystem.ParticleType.HitSpark);

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
                    partUtil.CreateRequest(idx, commandBuffer, pos.Value, 
                        ParticleRequestSystem.ParticleType.Explosion);
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
    EndSimulationEntityCommandBufferSystem commandBufferSystem;
    ParticleRequestSystem partReqSystem;

    // phys systems set callbacks up with
    BuildPhysicsWorld buildPhysWorld;
    StepPhysicsWorld stepPhysWorld;

    // entity groups
    EntityQuery playerGroup;
    EntityQuery enemyGroup;
    EntityQuery playerBulletGroup;
    EntityQuery enemyBulletGroup;

    // generalizing single player hit in case of multiplayer
    NativeMultiHashMap<Entity, CollisionInfo> playerToBulletCollisions;
    NativeMultiHashMap<Entity, CollisionInfo> enemyToBulletCollisions;

    NativeHashMap<Entity, BulletDamage> playerBulletDamageMap;

    // physics layer masks
    uint playerMask;
    uint enemyMask;
    uint playerBulletMask;
    uint enemyBulletMask;

    protected override void OnCreateManager()
    {
        // get other systems
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        partReqSystem = World.GetOrCreateSystem<ParticleRequestSystem>();
        buildPhysWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        stepPhysWorld = World.GetOrCreateSystem<StepPhysicsWorld>();

        playerGroup = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<Player>()
                }
            });
        enemyGroup = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<Enemy>(),
                    typeof(Health),
                    ComponentType.ReadOnly<Translation>()
                }
            });
        playerBulletGroup = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<PlayerBullet>(),
                    ComponentType.ReadOnly<BulletDamage>()
                }
            });
        enemyBulletGroup = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<EnemyBullet>(),
                    ComponentType.ReadOnly<BulletDamage>()
                }
            });

        playerMask = 1 << 3;
        enemyMask = 1 << 1;
        playerBulletMask = 1 << 2;
        enemyBulletMask = 1 << 0;
    }

    private void DisposeContainers(){
        if(playerToBulletCollisions.IsCreated){
            playerToBulletCollisions.Dispose();
        }
        if(enemyToBulletCollisions.IsCreated){
            enemyToBulletCollisions.Dispose();
        }
        if(playerBulletDamageMap.IsCreated){
            playerBulletDamageMap.Dispose();
        }
    }

    protected override void OnStopRunning()
    {
        DisposeContainers();
    }

    protected override JobHandle OnUpdate(JobHandle handle){

        // delegate for physics to add my jobs to its pipeline
        SimulationCallbacks.Callback callback = (ref ISimulation sim, 
            ref PhysicsWorld world, JobHandle deps) =>
        {
            // delete containers from previous frame
            DisposeContainers();

            JobHandle playerToBullet = InitPlayerToBulletHitJob(ref sim, ref world, deps);
            JobHandle enemyToBullet = InitEnemyToBulletHitJob(ref sim, ref world, deps);

            return JobHandle.CombineDependencies(playerToBullet, enemyToBullet);
        };

        stepPhysWorld.EnqueueCallback(SimulationCallbacks.Phase.PostCreateContacts, 
            callback);

        return handle;
    }

    private JobHandle InitPlayerToBulletHitJob(ref ISimulation sim, ref PhysicsWorld world, 
            JobHandle deps){

        // first arg is max capacity, setting as max possible collision pairs
        playerToBulletCollisions = new NativeMultiHashMap<Entity, CollisionInfo>(
            enemyBulletGroup.CalculateLength() * playerGroup.CalculateLength(), 
            Allocator.TempJob);
        
        JobHandle collectJob = new CollectHitsJob{
            world = buildPhysWorld.PhysicsWorld.CollisionWorld,
            collisions = playerToBulletCollisions.ToConcurrent(),
            targetMask = playerMask,
            bulletMask = enemyBulletMask
        }.Schedule(sim, ref world, deps);

        JobHandle processJob = new ProcessPlayerToBulletHits{
            commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            world = buildPhysWorld.PhysicsWorld.CollisionWorld,
            partUtil = partReqSystem.Util
        }.Schedule(playerToBulletCollisions, 10, collectJob);

        commandBufferSystem.AddJobHandleForProducer(processJob);
        return processJob;
    }

    private JobHandle InitEnemyToBulletHitJob(ref ISimulation sim, ref PhysicsWorld world, 
            JobHandle deps){

        // first arg is max capacity, setting as max possible collision pairs
        enemyToBulletCollisions = new NativeMultiHashMap<Entity, CollisionInfo>(
            playerBulletGroup.CalculateLength() * enemyGroup.CalculateLength(), 
            Allocator.TempJob);
        playerBulletDamageMap = new NativeHashMap<Entity, BulletDamage>(
            playerBulletGroup.CalculateLength(),
            Allocator.TempJob);
        
        JobHandle copyDamageJob = new CopyBulletDamageJob{
            damageMap = playerBulletDamageMap.ToConcurrent()
        }.Schedule(playerBulletGroup, deps);

        JobHandle collectJob = new CollectHitsJob{
            world = buildPhysWorld.PhysicsWorld.CollisionWorld,
            collisions = enemyToBulletCollisions.ToConcurrent(),
            targetMask = enemyMask,
            bulletMask = playerBulletMask
        }.Schedule(sim, ref world, deps);

        JobHandle processJob = new ProcessEnemyToBulletHits{
            commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            partUtil = partReqSystem.Util,
            collisions = enemyToBulletCollisions,
            damageMap = playerBulletDamageMap
        }.Schedule(enemyGroup, JobHandle.CombineDependencies(copyDamageJob, collectJob));

        commandBufferSystem.AddJobHandleForProducer(processJob);
        return processJob;
    }
}