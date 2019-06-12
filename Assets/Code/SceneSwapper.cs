using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;				// World, EntityManager, Entity
using Unity.Collections;			// NativeArray
using UnityEngine.SceneManagement;	// SceneManager

public class SceneSwapper : MonoBehaviour
{

    public static SceneSwapper instance = null;

	[SerializeField] private string nextScene = null;
    
	public delegate void SceneExit();
	public static event SceneExit OnSceneExit;

	void Awake(){
    	if(nextScene == null){
    		Debug.LogError("nextScene not specified on " + gameObject.name);
    	}

        if(instance == null){
            instance = this;
        }
        else if(instance != this){
            Destroy(gameObject);
        }
	}

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space)){
            ExitScene();
		}
    }

    public void ExitScene(){

        // handle any scene exiting events if they exist
        if(OnSceneExit != null){
            OnSceneExit();
        }

        EntityManager entManager = World.Active.EntityManager;

        // end all jobs
        entManager.CompleteAllJobs();

        // delete all existing entites
        NativeArray<Entity> ents = entManager.GetAllEntities();
        foreach(Entity ent in ents){
            entManager.DestroyEntity(ent);
        }
        ents.Dispose();

        // reset for next scene
        instance = null;
        
        SceneManager.LoadScene(nextScene);
    }
}
