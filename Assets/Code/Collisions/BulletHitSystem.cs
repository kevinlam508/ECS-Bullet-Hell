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
        [WriteOnly] public NativeMultiHashMap<Entity, CollisionInfo>.Concurrent playerCollisions;

        public uint playerMask;
        public uint bulletMask;

        // sets max collisions to be num bullets * num players
        // but should have never been more than that in playerCollisions anyways
        public int count;
        public int cap;

        public unsafe void Execute(ref ModifiableContactHeader contactHeader, 
                ref ModifiableContactPoint contactPoint){

            int idxA = contactHeader.BodyIndexPair.BodyAIndex;
            int idxB = contactHeader.BodyIndexPair.BodyBIndex;
            RigidBody rbA = world.Bodies[idxA];
            RigidBody rbB = world.Bodies[idxB];

            int playerIdx = 0, bulletIdx = 0;
            Entity playerEnt = Entity.Null;
            if((rbA.Collider->Filter.BelongsTo & playerMask) != 0){
                playerIdx = idxA;
                bulletIdx = idxB;
                playerEnt = rbA.Entity;

            }
            else if((rbB.Collider->Filter.BelongsTo & playerMask) != 0){
                playerIdx = idxB;
                bulletIdx = idxA;
                playerEnt = rbB.Entity;
            }

            if(count < cap){
                playerCollisions.Add(playerEnt, new CollisionInfo{
                        bodyIdx = bulletIdx
                    });
                ++count;
            }
        }
    }

    //[BurstCompile]
    struct ProcessHitsJob : IJobNativeMultiHashMapVisitKeyValue<Entity, CollisionInfo>{

        // store commands to be processed outside of jobs
        [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;

        // the physics world
        [ReadOnly] public CollisionWorld world;

        public void ExecuteNext(Entity player, CollisionInfo hitInfo){
            // process the collision
            // get the index of the body the player hit
            int bodyIdx = hitInfo.bodyIdx;

            // get the body from the world, then entity from the body
            Entity other = world.Bodies[bodyIdx].Entity;

            // delete other entity for now
            commandBuffer.DestroyEntity(other.Index, other);
            //Reflect(other, hitInfo);
        }

    }

    // buffer to entity deletion
    EndSimulationEntityCommandBufferSystem commandBufferSystem;

    // phys systems set callbacks up with
    BuildPhysicsWorld buildPhysWorld;
    StepPhysicsWorld stepPhysWorld;

    // all entities that count as players
    EntityQuery playerGroup;

    // all entities that count as bullets
    EntityQuery bulletGroup;

    // map of player(s) to entities hit
    // generalizing single player hit in case of multiplayer
    NativeMultiHashMap<Entity, CollisionInfo> playerCollisions;

    uint playerMask;
    uint bulletMask;

    protected override void OnCreateManager()
    {
        // get other systems
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        buildPhysWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        stepPhysWorld = World.GetOrCreateSystem<StepPhysicsWorld>();

        playerGroup = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<Player>(), 
                    ComponentType.ReadOnly<PhysicsCollider>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<Translation>()}
            });

        bulletGroup = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<BulletHit>(), 
                    ComponentType.ReadOnly<PhysicsCollider>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<Translation>()}
            });

        playerMask = 1 << 3;
        bulletMask = 1 << 0;
    }

    private void DisposeContainers(){
        if(playerCollisions.IsCreated){
            playerCollisions.Dispose();
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

            // first arg is max capacity, setting as max possible collision pairs
            playerCollisions = new NativeMultiHashMap<Entity, CollisionInfo>(
                bulletGroup.CalculateLength() * playerGroup.CalculateLength(), 
                Allocator.TempJob);

            Debug.Log(playerCollisions.Capacity);

            JobHandle collectJob = new CollectHitsJob{
                world = buildPhysWorld.PhysicsWorld.CollisionWorld,
                playerCollisions = playerCollisions.ToConcurrent(),
                playerMask = playerMask,
                bulletMask = bulletMask,
                count = 0,
                cap = playerCollisions.Capacity
            }.Schedule(sim, ref world, deps);

            JobHandle processJob = new ProcessHitsJob{
                commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                world = buildPhysWorld.PhysicsWorld.CollisionWorld
            }.Schedule(playerCollisions, 1, collectJob);
            commandBufferSystem.AddJobHandleForProducer(processJob);

            return processJob;
        };

        stepPhysWorld.EnqueueCallback(SimulationCallbacks.Phase.PostCreateContacts, 
            callback);

        return handle;
    }
}