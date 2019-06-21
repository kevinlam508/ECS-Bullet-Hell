using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;				// ComponentSystem
using Unity.Collections;            // NativeArray
using Unity.Transforms;             // Translation
using Unity.Mathematics;            // float3
using System;                       // Enum

public class ParticleRequestSystem : ComponentSystem
{

    public enum ParticleType{
        Explosion,
        HitSpark
    }

    public static class RequestUtility{

        public static void CreateRequest(int idx, EntityCommandBuffer.Concurrent buffer, 
                float3 position, ParticleType type){

            Entity req = buffer.CreateEntity(idx);
            buffer.AddComponent(idx, req, new Translation{ Value = position });
            buffer.AddComponent(idx, req, new ParticleRequest{ type = type });
        }

        public static void CreateRequest(EntityCommandBuffer buffer, 
                float3 position, ParticleType type){

            Entity req = buffer.CreateEntity();
            buffer.AddComponent(req, new Translation{ Value = position });
            buffer.AddComponent(req, new ParticleRequest{ type = type });
        }
    }

    // request processing
	EntityQuery requests;
    EndSimulationEntityCommandBufferSystem commandBufferSystem;

    // lazy object pool for particles
    string[] particleNames = {
        "Explosion", 
        "Hit Sparks"
    };
    List<ParticleSystemBundle> particleSystems = new List<ParticleSystemBundle>();

	protected override void OnCreateManager(){

        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        requests = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<ParticleRequest>(),
                    ComponentType.ReadOnly<Translation>()
                }
            });

        InitParticleSystems();
	}

    private void InitParticleSystems(){
        foreach(ParticleType p in Enum.GetValues(typeof(ParticleType))){
            GameObject go = GameObject.Instantiate(Resources.Load<GameObject>(particleNames[(int)p]));
            particleSystems.Add(go?.GetComponent<ParticleSystemBundle>());
        }

        // emit first volley to warm up system by emitting really far away
        foreach(ParticleSystemBundle bundle in particleSystems){
            if(bundle != null){
                ParticleSystem.EmitParams param = new ParticleSystem.EmitParams();
                param.position = new Vector3(-100, -100, -100);
                param.applyShapeToPosition = true;
                bundle.Emit(param);
            }
        }
    }

    private ParticleSystemBundle GetParticle(ParticleType p){

        ParticleSystemBundle res = particleSystems[(int)p];
        if(res == null){
            Debug.LogWarning("Particle " + p + " not found");
        }
        return res;
    }

	protected override void OnUpdate(){
        EntityCommandBuffer buffer = commandBufferSystem.CreateCommandBuffer();
        NativeArray<Entity> ents = requests.ToEntityArray(Allocator.TempJob);
        NativeArray<Translation> positions = requests.ToComponentDataArray<Translation>(Allocator.TempJob);
        NativeArray<ParticleRequest> reqs = requests.ToComponentDataArray<ParticleRequest>(Allocator.TempJob);

        for(int i = 0; i < ents.Length; ++i){
            buffer.DestroyEntity(ents[i]);

            // get the particles
            ParticleSystemBundle bundle = GetParticle(reqs[i].type);

            if(bundle != null){

                // play the particle system at the request's position
                ParticleSystem.EmitParams param = new ParticleSystem.EmitParams();
                param.position = positions[i].Value;
                param.applyShapeToPosition = true;
                bundle.Emit(param);
            }
        }

        // always dispose after using
        positions.Dispose();
        ents.Dispose();
        reqs.Dispose();
	}
}
