using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;						// Serializable

namespace EditableEnum{
	[CreateAssetMenu(fileName = "New Simple Enum", menuName = "Editable Enumerator/Simple Enumerator")]
	[Serializable]
	public class SimpleEnum : AbstractEnum<string>
	{
		protected override string GetValueName(int index){
	        return values[index];
	    }
	}
}
