using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "New Bullet Movement", menuName = "BulletMovement")]
[Serializable]
public class BulletMovementData : ScriptableObject{
	[Tooltip("Should not be ENUM_END, that is for debug purposes")]
    public BulletMovementSystem.MoveType moveType;

    [Tooltip("Movement in pixels per second")]
    public float moveSpeed;

    [Tooltip("Rotation in degrees per second")]
    public float rotateSpeed;

    public Material material;
}