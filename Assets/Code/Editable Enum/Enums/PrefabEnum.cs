using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;						// Serializable

namespace EditableEnum{
	[CreateAssetMenu(fileName = "New Prefab Enum", menuName = "Editable Enumerator/Prefab Enumerator")]
	[Serializable]
	public class PrefabEnum : AbstractUnityObjectEnum<GameObject>{}
}
