using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;              // Entity, EntityCommandBuffer
using Unity.Jobs;                  // IJob*, JobHandle
using Unity.Transforms;            // Translation, Rotation
using Unity.Burst;                 // BurstCompile
using Unity.Collections;           // ReadOnly
using Unity.Mathematics; 		   // math

using CustomConstants;			   // Constants

public class PlayerShootSystem : JobComponentSystem
{
	private EndSimulationEntityCommandBufferSystem commandBufferSystem;
    private EntityQuery players;

	protected override void OnCreateManager(){
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        // get entities that define players
        players = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<PlayerShoot>()
                }
            });
    }

    protected override JobHandle OnUpdate(JobHandle deps){
    	
    	JobHandle shootJob = new PlayerShootJob{
            commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
        	dt = Time.deltaTime,
            bulletHeight = Constants.PLAYER_BULLET_HEIGHT,
            timePassedBuffers = GetBufferFromEntity<TimePassed>(false),
            shooting = (Input.GetAxis("Fire1") > 0)
    	}.Schedule(players, deps);

        // tells buffer systems to wait for the job to finish, then
        //   it will perform the commands buffered
        commandBufferSystem.AddJobHandleForProducer(shootJob);

    	return shootJob;
    }

    struct PlayerShootJob : IJobForEachWithEntity<PlayerShoot, Translation, Rotation>{

    	// buffer for entity changing commands
    	public EntityCommandBuffer.Concurrent commandBuffer;

        // timing
        [NativeDisableParallelForRestriction]
        public BufferFromEntity<TimePassed> timePassedBuffers;
    	public float dt;

    	// making bullets
    	public float bulletHeight;
    	public bool shooting;

    	public void Execute(Entity ent, int index, [ReadOnly] ref PlayerShoot shoot, 
    			[ReadOnly] ref Translation pos, [ReadOnly] ref Rotation rotation){

    		// getting the right TimePassed
            DynamicBuffer<TimePassed> buffer = timePassedBuffers[ent];
            TimePassed timePassed = buffer[shoot.timeIdx];
			timePassed.time += dt;

            // fire until below period
            while(shooting && timePassed.time > shoot.shotCooldown){
                // shoot the pattern
                // buffers commands to do after thread completes
                CreateBullet(index, ref pos, ref rotation, shoot.bullet, 0);
        		timePassed.time -= shoot.shotCooldown;
            }

            buffer[shoot.timeIdx] = timePassed;
    	}

    	private void CreateBullet(int index, [ReadOnly] ref Translation position,
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
        }

    }
}
