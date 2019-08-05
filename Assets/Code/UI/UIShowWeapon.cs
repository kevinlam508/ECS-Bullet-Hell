using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIShowWeapon : MonoBehaviour
{

	[SerializeField] private PlayerStats stats = null;

	private List<GameObject> weaponVisuals = new List<GameObject>();

    // Start is called before the first frame update
    void Start()
    {
        foreach(Transform t in transform){
        	weaponVisuals.Add(t.gameObject);
        }
	    if(stats == null){
	    	Debug.LogWarning("Stats not set");
	    }
    }

    // Update is called once per frame
    void Update()
    {
    	if(stats != null){
	        foreach(PlayerStats.WeaponTypes weapon in System.Enum.GetValues(typeof(PlayerStats.WeaponTypes))){
	        	int idx = (int)weapon - 1; // -1 since flags start at 1
	        	if(0 <= idx && idx < weaponVisuals.Count){
	        		weaponVisuals[idx].SetActive(stats.activeWeapon == weapon);
	        	}
	        }
	    }
    }
}
