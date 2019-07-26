using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;						// Serializable

namespace EditableEnum{
	[CreateAssetMenu(fileName = "New AudioClip Enum", menuName = "Editable Enumerator/AudioClip Enumerator")]
	[Serializable]
	public class AudioClipEnum : AbstractUnityObjectEnum<AudioClip>{}
}
