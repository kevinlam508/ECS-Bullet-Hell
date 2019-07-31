using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;				// World, EntityManager, Entity
using Unity.Collections;			// NativeArray
using UnityEngine.SceneManagement;	// SceneManager

public class SceneSwapper : MonoBehaviour
{

    public static SceneSwapper instance = null;

	[SerializeField] private string[] scenes = {"Level Select"};
    
	public delegate void SceneExit();
	public static event SceneExit OnSceneExit;

    private int destinationScene = -1;
    private int exitCountdown = 1;

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

    void LateUpdate(){
        if(destinationScene >= 0){

            // exit 1 frame after starting exit to ensure there's not lingering issues
            if(exitCountdown == 0){
                ExitScene(destinationScene);
            }
            exitCountdown--;
        }
    }

    // Need to exit later because command buffer systems need to clear out first
    public void InitiateExit(int sceneIdx = 0){
        ActiveSystemManager.DisableAll();
        if(sceneIdx > scenes.Length){
            Debug.LogWarning("Scene index out of bounds: " + sceneIdx + ". Returning to Level Select.");
            destinationScene = 0;
        }
        else{
            destinationScene = sceneIdx;
        }
    }

    private void ExitScene(int sceneIdx = 0){

        EntityManager entManager = World.Active.EntityManager;

        // end all jobs
        entManager.CompleteAllJobs();

        // handle any scene exiting events if they exist
        if(OnSceneExit != null){
            OnSceneExit();
        }

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
        
        SceneManager.LoadScene(scenes[sceneIdx]);
    }
}
