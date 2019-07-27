using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;				// ComponentSystemBase

using System;						// Flags, Type

public partial class ActiveSystemManager : MonoBehaviour
{

    public static Dictionary<Type, SystemTypeFlags> systemTypes = new Dictionary<Type, SystemTypeFlags>();

    /*
     *	system: must be a type that extends ComponentSystemBase
     */
    public static void SetSystemType(Type system, SystemTypeFlags flag){
    	if(!system.IsSubclassOf(typeof(ComponentSystemBase))){
    		Debug.LogError(system + " is not a component system");
    	}
    	else if(systemTypes.ContainsKey(system)){
    		Debug.LogError(system + " is already registered");
    	}
    	else if(HasOneFlag(flag)){

    	}
    	else{
    		systemTypes.Add(system, flag);
    	}
    }

    private static bool HasOneFlag(SystemTypeFlags flags){
    	int numFlags = 0;
    	foreach(SystemTypeFlags flag in Enum.GetValues(typeof(SystemTypeFlags))){
    		if(flags.HasFlag(flag)){
    			++numFlags;
    		}
    	}

    	return numFlags == 0;
    }

    [SerializeField] private SystemTypeFlags activeSystems = SystemTypeFlags.None;

    void Awake(){
    	foreach(KeyValuePair<Type, SystemTypeFlags> pair in systemTypes){
    		World.Active.GetOrCreateSystem(pair.Key).Enabled = activeSystems.HasFlag(pair.Value);
    	}
    }
}
