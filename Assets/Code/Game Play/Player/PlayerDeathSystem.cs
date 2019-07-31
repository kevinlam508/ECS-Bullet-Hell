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

	protected override void OnCreateManager(){

        // get entities that define players
        players = GetEntityQuery(new EntityQueryDesc{
                All = new ComponentType[]{
                    ComponentType.ReadOnly<Player>()
                }
            });
    }

    protected override void OnUpdate(){
    	if(players.CalculateLength() == 0 && SceneSwapper.instance != null){
            SceneSwapper.instance.InitiateExit(1);
        }
    	
    }

}
