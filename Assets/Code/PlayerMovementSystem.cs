using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

public class PlayerMovementSystem : JobComponentSystem{
	struct PlayerMovementJob : IJobForEach<Translation, Player>{

		public float dx, dy, dt, speed;

		public void Execute(ref Translation position,[ReadOnly] ref Player player){
			position.Value += new float3(dx * speed * dt,
				dy * speed * dt, 0);
		}
	}

	// called on main thread, so can call Unity's static classes like Input
	protected override JobHandle OnUpdate(JobHandle handle){
        PlayerMovementJob job = new PlayerMovementJob{
            dx = Input.GetAxis("Horizontal"),
            dy = Input.GetAxis("Vertical"),
            dt = Time.deltaTime,
            speed = 5
        };

        return job.Schedule(this, handle);
    }
}
