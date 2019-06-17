using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSystemBundle : MonoBehaviour
{

	private List<ParticleSystem> particleSystems = new List<ParticleSystem>();

    void Awake()
    {
        foreach(Transform t in transform){
        	ParticleSystem part = t.gameObject.GetComponent<ParticleSystem>();
        	particleSystems.Add(part);
        }
    }

    public void Play(){
    	foreach(ParticleSystem system in particleSystems){
            system.Play();
        }
    }

    public bool IsPlaying{
        get{
            bool res = false;
            foreach(ParticleSystem system in particleSystems){
                res |= system.isPlaying;
            }
            return res;
        }
    }

    // look at max particles of the system if effects are not appearing
    public void Emit(ParticleSystem.EmitParams param){
        foreach(ParticleSystem system in particleSystems){
            ParticleSystem.Burst burst = system.emission.GetBurst(0);
            system.Emit(param, Random.Range(burst.minCount, burst.maxCount + 1));
        }
    }

    IEnumerator Test(){

        while(true){
            transform.RotateAround(transform.position, Vector3.forward, 30f);
            Play();
            yield return new WaitForSeconds(.1f);
        }
    }
}
