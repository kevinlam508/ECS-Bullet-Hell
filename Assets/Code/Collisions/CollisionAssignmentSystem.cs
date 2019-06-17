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
public class CollisionAssignmentSystem : JobComponentSystem
{
    // abstracted incase more data needed later
    struct CollisionInfo{
        public int bodyIdx;
        public float3 contactPos;
    }

    [BurstCompile]
    struct CollectHitsJob : IContactsJob{

        // the physics world
        [ReadOnly] public CollisionWorld world;

        // map of entities to hits for process hit job
        [WriteOnly] public NativeMultiHashMap<int, CollisionInfo>.Concurrent collisions;

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
                    collisions.Add(targetIdx, new CollisionInfo{
                            bodyIdx = bulletIdx,
                            contactPos = contactPoint.Position
                        });
                }
            }
        }
    }

    //[BurstCompile]
    struct ProcessPlayerToBulletHits : IJobNativeMultiHashMapVisitKeyValue<int, CollisionInfo>{

        // store commands to be processed outside of jobs
        [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;

        // the physics world
        [ReadOnly] public CollisionWorld world;
        public ParticleRequestSystem.ParticleRequestUtility partUtil;

        public void ExecuteNext(int playerBodyIdx, CollisionInfo data){
            // process the collision

            // get the entity of the other body
            RigidBody otherBody = world.Bodies[data.bodyIdx];
            Entity other = otherBody.Entity;

            // delete other entity for now
            commandBuffer.DestroyEntity(other.Index, other);
            partUtil.CreateRequest(other.Index, commandBuffer,
                data.contactPos, ParticleRequestSystem.ParticleType.HitSpark);
        }

    }

    struct ProcessEnemyToBulletHits : IJobNativeMultiHashMapVisitKeyValue<int, CollisionInfo>{

        // store commands to be processed outside of jobs
        [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;

        [ReadOnly] public CollisionWorld world;

        public void ExecuteNext(int enemyBodyIdx, CollisionInfo data){

            // get the entity of the enemy
            RigidBody enemyBody = world.Bodies[enemyBodyIdx];
            Entity enemy = enemyBody.Entity;

            // get the entity of the other body
            RigidBody otherBody = world.Bodies[data.bodyIdx];
            Entity other = otherBody.Entity;

            // set marker on the enemy
            DynamicBuffer<BulletHit> buffer = commandBuffer.SetBuffer<BulletHit>(enemy.Index, enemy);
            buffer.Add(new BulletHit{
                    bullet = other,
                    hitPos = data.contactPos
                });
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
    NativeMultiHashMap<int, CollisionInfo> playerToBulletCollisions;
    NativeMultiHashMap<int, CollisionInfo> enemyToBulletCollisions;

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
        playerToBulletCollisions = new NativeMultiHashMap<int, CollisionInfo>(
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
        enemyToBulletCollisions = new NativeMultiHashMap<int, CollisionInfo>(
            playerBulletGroup.CalculateLength() * enemyGroup.CalculateLength(), 
            Allocator.TempJob);
        
        JobHandle collectJob = new CollectHitsJob{
            world = buildPhysWorld.PhysicsWorld.CollisionWorld,
            collisions = enemyToBulletCollisions.ToConcurrent(),
            targetMask = enemyMask,
            bulletMask = playerBulletMask
        }.Schedule(sim, ref world, deps);

        JobHandle processJob = new ProcessEnemyToBulletHits{
            commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            world = buildPhysWorld.PhysicsWorld.CollisionWorld
        }.Schedule(enemyToBulletCollisions, 10, collectJob);

        commandBufferSystem.AddJobHandleForProducer(processJob);
        return processJob;
    }
}