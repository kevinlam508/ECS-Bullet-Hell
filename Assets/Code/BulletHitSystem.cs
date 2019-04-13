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
	struct BulletHitJob : IJobForEachWithEntity<BulletHit, PhysicsCollider, Translation, Rotation>{

		// stores deletions of entities after thread finishes
        public EntityCommandBuffer commandBuffer;

        // physics world to ask about collisions
        [ReadOnly] public CollisionWorld world;

		public unsafe void Execute(Entity ent, int index, [ReadOnly] ref BulletHit bulletHit,
				ref PhysicsCollider collider, [ReadOnly] ref Translation pos,
				[ReadOnly] ref Rotation rot){

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
            	commandBuffer.DestroyEntity(ent);
            }

		}
	}

	// buffer to entity deletion
	EndSimulationEntityCommandBufferSystem commandBufferSystem;

	// phys world that will be polled in job
    BuildPhysicsWorld createPhysicsWorldSystem;

	protected override void OnCreateManager()
    {
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        createPhysicsWorldSystem = World.GetOrCreateSystem<BuildPhysicsWorld>();
    }

	protected override JobHandle OnUpdate(JobHandle handle){
		JobHandle hitJobHandle = new BulletHitJob{
			commandBuffer = commandBufferSystem.CreateCommandBuffer(),
			world = createPhysicsWorldSystem.PhysicsWorld.CollisionWorld
		}.ScheduleSingle(this, handle);

		// tell bufferSystem to wait for hitJob, then it'll perform
		// buffered commands
        commandBufferSystem.AddJobHandleForProducer(hitJobHandle);

		return hitJobHandle;
	}
}