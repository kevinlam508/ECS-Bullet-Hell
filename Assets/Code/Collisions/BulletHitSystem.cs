using UnityEngine;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;      
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

    [BurstCompile]
    struct CollectHitsJob : IJobForEachWithEntity<PhysicsCollider, Translation, Rotation>{

        // physics world to ask about collisions
        [ReadOnly] public CollisionWorld world;

        // temp storage for hits
        public NativeList<DistanceHit> hits;

        // map of entities to hits for process hit job
        [WriteOnly] public NativeMultiHashMap<Entity, DistanceHit> collisions;

        public unsafe void Execute(Entity ent, int index, [ReadOnly] ref PhysicsCollider collider, 
                [ReadOnly] ref Translation pos, [ReadOnly] ref Rotation rot){

            hits.Clear();

            // params for the collision
            ColliderDistanceInput input = new ColliderDistanceInput
            {
                Collider = collider.ColliderPtr,
                Transform = new RigidTransform(rot.Value, pos.Value),
                MaxDistance = 0
            };

            // poll for nearest collisions with player
            world.CalculateDistance(input, ref hits);

            for(int i = 0; i < hits.Length; ++i){
                collisions.Add(ent, hits[i]);
            }

        }
    }

    //[BurstCompile]
    struct ProcessHitsJob : IJobNativeMultiHashMapVisitKeyValue<Entity, DistanceHit>{

        // store commands to be processed outside of jobs
        [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;

        // the physics world
        [ReadOnly] public CollisionWorld world;

        public void ExecuteNext(Entity player, DistanceHit hitInfo){
            // process the collision
            // get the index of the body the player hit
            int bodyIdx = hitInfo.RigidBodyIndex;

            // get the body from the world, then entity from the body
            Entity other = world.Bodies[bodyIdx].Entity;

            // delete other entity for now
            //commandBuffer.DestroyEntity(other.Index, other);
            Reflect(other, hitInfo);
        }

        private void Reflect(Entity ent, DistanceHit hitInfo){
            commandBuffer.AddComponent(ent.Index, ent, 
                new DelayedReflection{
                    fraction = hitInfo.Fraction,
                    reflectNorm = hitInfo.SurfaceNormal
                    });
        }

    }

    // buffer to entity deletion
    EndSimulationEntityCommandBufferSystem commandBufferSystem;

    // phys world that will be polled in job
    BuildPhysicsWorld createPhysicsWorldSystem;

    // all entities that count as players
    EntityQuery playerGroup;

    // all entities that count as bullets
    EntityQuery bulletGroup;

    // temp storage to collect hits, alloc once and reuse space
    NativeList<DistanceHit> hits;

    // map of player(s) to entities hit
    // generalizing single player hit in case of multiplayer
    NativeMultiHashMap<Entity, DistanceHit> collisions;


    protected override void OnCreateManager()
    {
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        createPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();

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
    }

    private void DisposeContainers(){
        if(hits.IsCreated){
            hits.Dispose();
        }
        if(collisions.IsCreated){
            collisions.Dispose();
        }
    }

    protected override void OnStopRunning()
    {
        DisposeContainers();
    }

    protected override JobHandle OnUpdate(JobHandle handle){

        DisposeContainers();

        // both conatainers will only last for the jobs, so using TempJob alloc
        hits = new NativeList<DistanceHit>(100, Allocator.TempJob);

        // first arg is max capacity, setting as max possible collision pairs
        collisions = new NativeMultiHashMap<Entity, DistanceHit>(
            bulletGroup.CalculateLength() * playerGroup.CalculateLength(), 
            Allocator.TempJob);

        JobHandle hitJobHandle = new CollectHitsJob{
            world = createPhysicsWorldSystem.PhysicsWorld.CollisionWorld,
            hits = hits,
            collisions = collisions
        }.ScheduleSingle(playerGroup, handle);

        JobHandle processJobHandle = new ProcessHitsJob{
            commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            world = createPhysicsWorldSystem.PhysicsWorld.CollisionWorld
        }.Schedule(collisions, 64, hitJobHandle);

        // tell bufferSystem to wait for the process job, then it'll perform
        // buffered commands
        commandBufferSystem.AddJobHandleForProducer(processJobHandle);

        return processJobHandle;
    }
}