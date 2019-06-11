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

using CollisionEvent = Unity.Physics.LowLevel.CollisionEvent;

[UpdateAfter(typeof(BulletMovementSystem))]
[UpdateAfter(typeof(PlayerMovementSystem))]
[UpdateAfter(typeof(BuildPhysicsWorld))]
[UpdateBefore(typeof(StepPhysicsWorld))]
public class BulletHitSystem : JobComponentSystem
{
    // abstracted incase more data needed later
    struct CollisionInfo{
        public int bodyIdx;
    }

    [BurstCompile]
    struct CollectHitsJob : IContactsJob{

        // the physics world
        [ReadOnly] public CollisionWorld world;

        // map of entities to hits for process hit job
        [WriteOnly] public NativeMultiHashMap<Entity, CollisionInfo>.Concurrent collisions;

        public uint targetMask;
        public uint bulletMask;

        // should have never been more than that in collisions anyways
        public int count;
        public int cap;

        public unsafe void Execute(ref ModifiableContactHeader contactHeader, 
                ref ModifiableContactPoint contactPoint){

            int idxA = contactHeader.BodyIndexPair.BodyAIndex;
            int idxB = contactHeader.BodyIndexPair.BodyBIndex;
            RigidBody rbA = world.Bodies[idxA];
            RigidBody rbB = world.Bodies[idxB];

            int targetIdx = 0, bulletIdx = 0;
            Entity targetEnt = Entity.Null;
            if((rbA.Collider->Filter.BelongsTo & targetMask) != 0
                    && (rbB.Collider->Filter.BelongsTo & bulletMask) != 0){
                targetIdx = idxA;
                bulletIdx = idxB;
                targetEnt = rbA.Entity;

            }
            else if((rbB.Collider->Filter.BelongsTo & targetMask) != 0
                    && (rbA.Collider->Filter.BelongsTo & bulletMask) != 0){
                targetIdx = idxB;
                bulletIdx = idxA;
                targetEnt = rbB.Entity;
            }

            if(count < cap && targetEnt != Entity.Null){
                collisions.Add(targetEnt, new CollisionInfo{
                        bodyIdx = bulletIdx
                    });
                ++count;
            }
        }
    }

    //[BurstCompile]
    struct ProcessPlayerToBulletHits : IJobNativeMultiHashMapVisitKeyValue<Entity, CollisionInfo>{

        // store commands to be processed outside of jobs
        [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;

        // the physics world
        [ReadOnly] public CollisionWorld world;

        public void ExecuteNext(Entity player, CollisionInfo data){
            // process the collision
            // get the index of the body the player hit
            int bodyIdx = data.bodyIdx;

            // get the body from the world, then entity from the body
            Entity other = world.Bodies[bodyIdx].Entity;

            // delete other entity for now
            commandBuffer.DestroyEntity(other.Index, other);
            //Reflect(other, hitInfo);
        }

    }

    struct ProcessEnemyToBulletHits : IJobNativeMultiHashMapVisitKeyValue<Entity, CollisionInfo>{

        // store commands to be processed outside of jobs
        [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;

        // the physics world
        [ReadOnly] public CollisionWorld world;

        public void ExecuteNext(Entity enemy, CollisionInfo data){
            // process the collision
            // get the index of the body the player hit
            int bodyIdx = data.bodyIdx;

            // get the body from the world, then entity from the body
            Entity other = world.Bodies[bodyIdx].Entity;

            // delete other entity for now
            commandBuffer.DestroyEntity(other.Index, other);
            commandBuffer.DestroyEntity(enemy.Index, enemy);
            //Reflect(other, hitInfo);
        }

    }

    // buffer to entity deletion
    EndSimulationEntityCommandBufferSystem commandBufferSystem;

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

    // physics layer masks
    uint playerMask;
    uint enemyMask;
    uint playerBulletMask;
    uint enemyBulletMask;

    protected override void OnCreateManager()
    {
        // get other systems
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        buildPhysWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        stepPhysWorld = World.GetOrCreateSystem<StepPhysicsWorld>();

        playerGroup = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<Player>()
                }
            });
        enemyGroup = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<Enemy>()
                }
            });
        playerBulletGroup = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<PlayerBullet>()
                }
            });
        enemyBulletGroup = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<EnemyBullet>()
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
            bulletMask = enemyBulletMask,
            count = 0,
            cap = playerToBulletCollisions.Capacity
        }.Schedule(sim, ref world, deps);

        JobHandle processJob = new ProcessPlayerToBulletHits{
            commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            world = buildPhysWorld.PhysicsWorld.CollisionWorld
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
        
        JobHandle collectJob = new CollectHitsJob{
            world = buildPhysWorld.PhysicsWorld.CollisionWorld,
            collisions = enemyToBulletCollisions.ToConcurrent(),
            targetMask = enemyMask,
            bulletMask = playerBulletMask,
            count = 0,
            cap = enemyToBulletCollisions.Capacity
        }.Schedule(sim, ref world, deps);

        JobHandle processJob = new ProcessEnemyToBulletHits{
            commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            world = buildPhysWorld.PhysicsWorld.CollisionWorld
        }.Schedule(enemyToBulletCollisions, 10, collectJob);

        commandBufferSystem.AddJobHandleForProducer(processJob);
        return processJob;
    }
}