using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Entities;				// Entity

[CreateAssetMenu(fileName = "New Player Stats", menuName = "Player Stats")]
public partial class PlayerStats : ScriptableObject
{
	public int maxHealth;
	public int currentHealth;

	public WeaponTypes firstWeapon;
	public WeaponTypes secondWeapon;
	public WeaponTypes activeWeapon;

	public static Dictionary<Entity, PlayerStats> statsMap = new Dictionary<Entity, PlayerStats>();
	static PlayerStats(){
		SceneSwapper.OnSceneExit += statsMap.Clear;
	}

	public static void RegisterStats(Entity ent, PlayerStats stats){
		statsMap.Add(ent, stats);
	}

}
