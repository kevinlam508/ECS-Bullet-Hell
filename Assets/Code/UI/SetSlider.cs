using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;

public class SetSlider : MonoBehaviour
{
    public PlayerStats stats = null;
    public bool showHealth = true;
    private Slider slider = null;

    void Awake(){
    	slider = GetComponent<Slider>();
    }

    void Update(){
    	if(showHealth){
            slider.maxValue = stats.maxHealth;
    		slider.value = stats.currentHealth;
    	}
    	else{
            if(stats.remainingBoostDuration <= 0){
                slider.maxValue = stats.maxCharges; 
                slider.value = stats.boostCharges;
            }
            else{
                slider.maxValue = stats.boostDuration; 
                slider.value = stats.remainingBoostDuration;
            }
    	}
    }
}
