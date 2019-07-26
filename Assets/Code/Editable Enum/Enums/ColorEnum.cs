using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;						// Serializable

namespace EditableEnum{
	[CreateAssetMenu(fileName = "New Color Enum", menuName = "Editable Enumerator/Color Enumerator")]
	[Serializable]
	public class ColorEnum : AbstractPairEnum<Color, ColorEnum.ColorPair>{

		[Serializable]
		public class ColorPair : AbstractPair<Color>{}
	}
}