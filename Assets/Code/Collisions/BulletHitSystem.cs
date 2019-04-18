using UnityEngine;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

[UpdateAfter(typeof(BulletMovementSystem))]
[UpdateAfter(typeof(PlayerMovementSystem))]
[UpdateAfter(typeof(BuildPhysicsWorld))]
[UpdateBefore(typeof(StepPhysicsWorld))]
public class BulletHitSystem : JobComponentSystem
{

	[BurstCompile]
	struct BulletHitJob : IJobForEachWithEntity<PhysicsCollider, Translation, Rotation>{

		// stores deletions of entities after thread finishes
		// Concurrent version allows this job to run on multiple threads
        public EntityCommandBuffer.Concurrent commandBuffer;

        // physics world to ask about collisions
        [ReadOnly] public CollisionWorld world;

		public unsafe void Execute(Entity ent, int index, [ReadOnly] ref PhysicsCollider collider, 
				[ReadOnly] ref Translation pos, [ReadOnly] ref Rotation rot){

			// params for the collision
			ColliderDistanceInput input = new ColliderDistanceInput
            {
                Collider = collider.ColliderPtr,
                Transform = new RigidTransform(rot.Value, pos.Value),
                MaxDistance = 0
            };
            DistanceHit hit;

            // poll for nearest collision
            if(world.CalculateDistance(input, out hit)){
            	commandBuffer.DestroyEntity(index, ent);
            }

		}
	}

	// buffer to entity deletion
	EndSimulationEntityCommandBufferSystem commandBufferSystem;

	// phys world that will be polled in job
    BuildPhysicsWorld createPhysicsWorldSystem;

    EntityQuery bullets;

	protected override void OnCreateManager()
    {
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        createPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();

        bullets = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                	ComponentType.ReadOnly<BulletHit>(), 
                    ComponentType.ReadOnly<PhysicsCollider>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<Translation>()}
            });
    }

	protected override JobHandle OnUpdate(JobHandle handle){
		JobHandle hitJobHandle = new BulletHitJob{
			commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
			world = createPhysicsWorldSystem.PhysicsWorld.CollisionWorld
		}.Schedule(bullets, handle);

		// tell bufferSystem to wait for hitJob, then it'll perform
		// buffered commands
        commandBufferSystem.AddJobHandleForProducer(hitJobHandle);

		return hitJobHandle;
	}
}