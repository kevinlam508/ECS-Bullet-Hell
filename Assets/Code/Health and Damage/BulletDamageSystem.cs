using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;			 // EntityQuery
using Unity.Jobs;                // job interfaces, JobComponentSystem
using Unity.Collections;         // NativeHashMap
using Unity.Burst;
using Unity.Transforms;			 // Translation

public class BulletDamageSystem : JobComponentSystem
{
    // other systems
    EndSimulationEntityCommandBufferSystem commandBufferSystem;
    ParticleRequestSystem partReqSystem;

    // groups of entities to work on
	EntityQuery playerBulletGroup;
	EntityQuery enemyGroup;

	NativeHashMap<Entity, BulletDamage> bulletDamageMap;

    protected override void OnCreateManager()
    {
		// get other systems
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        partReqSystem = World.GetOrCreateSystem<ParticleRequestSystem>();

        // get entity groups
        enemyGroup = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<Enemy>(),
                    typeof(BulletHit),
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
    }

    private void DisposeContainers(){
        if(bulletDamageMap.IsCreated){
            bulletDamageMap.Dispose();
        }
    }

    protected override void OnStopRunning()
    {
        DisposeContainers();
    }

    protected override JobHandle OnUpdate(JobHandle handle){

    	DisposeContainers();
    	bulletDamageMap = new NativeHashMap<Entity, BulletDamage>(
    		playerBulletGroup.CalculateLength(), Allocator.TempJob);

    	JobHandle copyJob = new CopyDamageJob{
    		damageMap = bulletDamageMap.ToConcurrent()
    	}.Schedule(playerBulletGroup, handle);

    	JobHandle computeJob = new ComputeDamageJob{
    		commandBuffer = commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
    		damageMap = bulletDamageMap,
    		partUtil = partReqSystem.Util,
    		bulletHitBuffers = GetBufferFromEntity<BulletHit>(false)
    	}.Schedule(enemyGroup, copyJob);

        return computeJob;
    }

    struct CopyDamageJob : IJobForEachWithEntity<BulletDamage>{

    	[WriteOnly] public NativeHashMap<Entity, BulletDamage>.Concurrent damageMap;

    	public void Execute(Entity ent, int idx, [ReadOnly] ref BulletDamage damage){
    		damageMap.TryAdd(ent, damage);
    	}
    }

    struct ComputeDamageJob : IJobForEachWithEntity<Health, Translation>{

        [WriteOnly] public EntityCommandBuffer.Concurrent commandBuffer;
    	[ReadOnly] public NativeHashMap<Entity, BulletDamage> damageMap;
        public ParticleRequestSystem.ParticleRequestUtility partUtil;

        // do this rather than commandBuffer.SetBuffer() to be able to read
        //   from the buffer
        [NativeDisableParallelForRestriction]
        public BufferFromEntity<BulletHit> bulletHitBuffers;

        public void Execute(Entity ent, int idx, ref Health health, ref Translation pos){

        	// collect total damage
        	int totalDamage = 0;
        	DynamicBuffer<BulletHit> hits = bulletHitBuffers[ent];
        	for(int i = 0; i < hits.Length; ++i){
        		BulletHit hit = hits[i];

        		// attempt to get info because collision system may have added
        		//   a hit for a bullet destroyed last frame
        		BulletDamage damageInfo;
        		if(damageMap.TryGetValue(hit.bullet, out damageInfo)){
	        		totalDamage += damageInfo.damage;
	        		partUtil.CreateRequest(idx, commandBuffer, hit.hitPos, 
	        			ParticleRequestSystem.ParticleType.HitSpark);

	        		// delete for now, handle bullet death stuff in the future
	        		commandBuffer.DestroyEntity(idx, hit.bullet);
	        	}
        	}
        	hits.Clear();

        	// deal damage and die
        	health.health -= totalDamage;
        	if(health.health <= 0){
        		commandBuffer.DestroyEntity(idx, ent);
        		partUtil.CreateRequest(idx, commandBuffer, pos.Value, 
        			ParticleRequestSystem.ParticleType.Explosion);
        	}
        }
    }
}
