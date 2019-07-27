using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Entities;               // IBufferElementData

/*
 *  Made this a buffer element so that there can be multiple versions of this
 *    component type.
 *  In this case, another component will use a single element from the buffer
 */
[InternalBufferCapacity(1)]
public struct TimePassed : IBufferElementData
{
    public float time;
}

public class TimePassedUtility{

	// returns the index of the new buffer
	public static int AddDefault(Entity ent, EntityManager manager){
		DynamicBuffer<TimePassed> buffer;
        if(manager.HasComponent<TimePassed>(ent)){
            buffer = manager.GetBuffer<TimePassed>(ent);
        }
        else{
            buffer = manager.AddBuffer<TimePassed>(ent);
        }
        int idx = buffer.Length;
        buffer.Add(new TimePassed{ time = 0 });

        return idx;
	}
}