﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;				// ComponentSystem
using Unity.Collections;            // NativeArray
using Unity.Transforms;             // Translation
using Unity.Mathematics;            // float3
using Unity.Jobs;
using System;                       // Enum

 // hacky, meant to put after EndSimulationEntityCommandBufferSystem to minimize sync point, but this is close enough
[UpdateAfter(typeof(WaveSpawningSystem))]
[SystemType(ActiveSystemManager.SystemTypes.Stage)]
public partial class EffectRequestSystem : ComponentSystem
{
    public struct RequestUtility{

        public NativeQueue<ParticleRequest>.ParallelWriter particleQueue;

        public void CreateParticleRequest(float3 position, ParticleType type){
            particleQueue.Enqueue(new ParticleRequest{
                    position = position,
                    type = type
                });
        }
    }

    public RequestUtility GetUtility(){
        particleRequestQueues.Add(new NativeQueue<ParticleRequest>(Allocator.TempJob));
        return new RequestUtility{
            particleQueue = particleRequestQueues[particleRequestQueues.Count - 1].AsParallelWriter()
        };
    }

    JobHandle deps = new JobHandle();

    public void AddDependency(JobHandle dependence){
        deps = JobHandle.CombineDependencies(deps, dependence);
    }

	protected override void OnCreate(){
        InitParticleSystems();
	}

    protected override void OnStartRunning(){
        ClearSceneData();
        InitParticleSystems();
    }

    protected override void OnUpdate(){
        deps.Complete();
        deps = new JobHandle();
        ProcessParticleRequests();
    }

    protected override void OnStopRunning(){
        DisposeContainers();
    }

    protected override void OnDestroy(){
        DisposeContainers();
    }

    private void DisposeContainers(){
        deps.Complete();
        foreach(NativeQueue<ParticleRequest> particleRequestQueue in particleRequestQueues){
            if(particleRequestQueue.IsCreated){
                particleRequestQueue.Dispose();
            }
        }
        particleRequestQueues.Clear();
    }

    private void ClearSceneData(){
        particleSystems.Clear();
    }

	/*
     *  ========================================================================
	 *	Particle Effects
     *  ========================================================================
	 */

    private EditableEnum.PrefabEnum particleEnum;
    private List<ParticleSystemBundle> particleSystems = new List<ParticleSystemBundle>();
    private List<NativeQueue<ParticleRequest>> particleRequestQueues
        = new List<NativeQueue<ParticleRequest>>();

	public struct ParticleRequest{
		public float3 position;
		public ParticleType type;
	}

    private void InitParticleSystems(){
        particleEnum = (EditableEnum.PrefabEnum)Resources.Load(
        	"Particle Effects/ParticleType");

        foreach(GameObject go in particleEnum.values){
        	GameObject newGo = GameObject.Instantiate(go);
        	particleSystems.Add(newGo?.GetComponent<ParticleSystemBundle>());
        }

        // emit first volley to warm up system by emitting really far away
        //   since first emit doesn't properly use scale, blame Unity
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

    private void ProcessParticleRequests(){
        foreach(NativeQueue<ParticleRequest> particleRequestQueue in particleRequestQueues){
            while(particleRequestQueue.TryDequeue(out ParticleRequest req)){
                // get the particles
                ParticleSystemBundle bundle = GetParticle(req.type);

                if(bundle != null){
                    // play the particle system at the request's position
                    ParticleSystem.EmitParams param = new ParticleSystem.EmitParams();
                    param.position = req.position;
                    param.applyShapeToPosition = true;
                    bundle.Emit(param);
                }
            }
            particleRequestQueue.Dispose();
        }
        particleRequestQueues.Clear();
    }
}
