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

[AlwaysUpdateSystem]
[SystemType(ActiveSystemManager.SystemTypes.Stage)]
public class PlayerDeathSystem : ComponentSystem
{
    private EntityQuery players;

	protected override void OnCreate(){

        // get entities that define players
        players = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<Player>(),
                    ComponentType.ReadOnly<Health>()
                }
            });
    }

    protected override void OnUpdate(){

        NativeArray<Entity> ents = players.ToEntityArray(Allocator.TempJob);

    	if(ents.Length == 0 && SceneSwapper.instance != null){
            SceneSwapper.instance.InitiateExit(1);
        }
        else if(ents.Length > 0){
            NativeArray<Health> healths = players.ToComponentDataArray<Health>(Allocator.TempJob);

            for(int i = 0; i < ents.Length; ++i){
                if(PlayerStats.statsMap.ContainsKey(ents[i])){
                    PlayerStats.statsMap[ents[i]].currentHealth = healths[i].health;
                }
            }

            healths.Dispose();
        }
        ents.Dispose();
    	
    }
}
