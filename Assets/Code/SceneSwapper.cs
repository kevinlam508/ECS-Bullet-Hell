using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;				// World, EntityManager, Entity
using Unity.Collections;			// NativeArray
using UnityEngine.SceneManagement;	// SceneManager

public class SceneSwapper : MonoBehaviour
{
	[SerializeField] private string nextScene = null;
    
	public delegate void SceneExit();
	public static event SceneExit OnSceneExit;

	void Awake(){
    	if(nextScene == null){
    		Debug.LogError("nextScene not specified on " + gameObject.name);
    	}
	}

    // Update is called once per frame
    void Update()
    {

        if(Input.GetKeyDown(KeyCode.Space)){

        	// handle any scene exiting events if they exist
        	if(OnSceneExit != null){
        		OnSceneExit();
        	}

        	// delete all existing entites
            EntityManager entManager = World.Active.EntityManager;
            NativeArray<Entity> ents = entManager.GetAllEntities();
            foreach(Entity ent in ents){
            	entManager.DestroyEntity(ent);
            }
            ents.Dispose();

			SceneManager.LoadScene(nextScene);
		}
    }
}
