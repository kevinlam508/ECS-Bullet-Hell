using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;				// World, EntityManager, Entity
using Unity.Collections;			// NativeArray
using UnityEngine.SceneManagement;	// SceneManager

public class SceneSwapper : MonoBehaviour
{

    public static SceneSwapper instance = null;

	[SerializeField] private string[] scenes = {"Dummy"};
    
	public delegate void SceneExit();
	public static event SceneExit OnSceneExit;

	void Awake(){
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
        ExitScene(0);
    }

    public void ExitScene(int sceneIdx){
        if(sceneIdx > scenes.Length){
            Debug.LogWarning("Scene index out of bounds: " + sceneIdx);
        }

        // handle any scene exiting events if they exist
        if(OnSceneExit != null){
            OnSceneExit();
        }

        EntityManager entManager = World.Active.EntityManager;

        // end all jobs
        entManager.CompleteAllJobs();

        // delete all existing entites
        NativeArray<Entity> ents = entManager.GetAllEntities();
        EntityCommandBuffer buffer = World.Active.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>()
            .CreateCommandBuffer();
        foreach(Entity ent in ents){
            buffer.DestroyEntity(ent);
        }
        ents.Dispose();

        // reset for next scene
        instance = null;
        
        SceneManager.LoadScene(scenes[0]);
    }
}
