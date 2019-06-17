using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;						// Serializable

[CreateAssetMenu(fileName = "New Bullet Damage", menuName = "BulletDamage")]
[Serializable]
public class BulletDamageData : ScriptableObject{

    [Tooltip("Damage this bullet will deal")]
    public int damage;

    [Tooltip("Number of hits this bullet will survive through")]
    public int pierceCount;

    public BulletDamage ToBulletDamage(){
    	return new BulletDamage{
    		damage = damage,
    		pierceCount = pierceCount
    	};
    }
}
