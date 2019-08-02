using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;

public class SetSliderHealth : MonoBehaviour
{
    public PlayerStats stats = null;
    private Slider slider = null;

    void Awake(){
    	slider = GetComponent<Slider>();
    	slider.maxValue = stats.maxHealth;
    }

    void Update(){
    	slider.value = stats.currentHealth;
    }
}
