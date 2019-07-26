using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;						// Serializable

namespace EditableEnum{
	[CreateAssetMenu(fileName = "New Sprite Enum", menuName = "Editable Enumerator/Sprite Enumerator")]
	[Serializable]
	public class SpriteEnum : AbstractUnityObjectEnum<Sprite>{}
}
