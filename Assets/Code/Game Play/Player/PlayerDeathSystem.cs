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
public class PlayerDeathSystem : ComponentSystem
{

    static PlayerDeathSystem(){
        ActiveSystemManager.SetSystemType(typeof(PlayerDeathSystem), 
            ActiveSystemManager.SystemTypeFlags.Stage);
    }

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

        // TODO: add conditions to check if in level
    	if(players.CalculateLength() == 0 && SceneSwapper.instance != null){
            SceneSwapper.instance.ExitScene(1);
        }
    	
    }

}
