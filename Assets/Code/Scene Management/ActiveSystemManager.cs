using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;				// ComponentSystemBase

using System;						// Flags, Type
using System.Reflection;

public partial class ActiveSystemManager : MonoBehaviour
{

    private static Dictionary<Type, SystemTypes> systemTypes = new Dictionary<Type, SystemTypes>();
    private static Dictionary<Type, ComponentSystemBase> instanceCache = new Dictionary<Type, ComponentSystemBase>(); 

    static ActiveSystemManager(){
        foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies()){
            foreach(Type t in a.GetTypes()){
                SystemTypeAttribute[] attributes = (SystemTypeAttribute[])t
                    .GetCustomAttributes(typeof(SystemTypeAttribute), true);

                if(attributes.Length > 0){
                    SetSystemType(t, attributes[0].Flag);
                }
            }
        }
    }

    /*
     *	system: must be a type that extends ComponentSystemBase
     */
    private static void SetSystemType(Type system, SystemTypes flag){
    	if(!system.IsSubclassOf(typeof(ComponentSystemBase))){
    		Debug.LogError(system + " is not a component system.");
    	}
    	else if(HasOneFlag(flag)){
            Debug.LogError(flag + " on " + system +" is not a single flag.");
    	}
    	else{
    		systemTypes.Add(system, flag);
    	}
    }

    private static bool HasOneFlag(SystemTypes flags){
    	int numFlags = 0;
    	foreach(SystemTypes flag in Enum.GetValues(typeof(SystemTypes))){
    		if(flag != SystemTypes.None && flags.HasFlag(flag)){
    			++numFlags;
    		}
    	}

    	return numFlags == 0;
    }

    [SerializeField] private SystemTypes activeSystems = SystemTypes.None;

    void Awake(){
    	foreach(KeyValuePair<Type, SystemTypes> pair in systemTypes){
            if(!instanceCache.ContainsKey(pair.Key)){
                instanceCache.Add(pair.Key, World.Active.GetOrCreateSystem(pair.Key));
            }
    		instanceCache[pair.Key].Enabled = activeSystems.HasFlag(pair.Value);
    	}
    }
}
