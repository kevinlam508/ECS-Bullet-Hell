using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;						// Serializable


namespace EditableEnum{
	/*
	 * Class to make enumerators for values don't inherit from UnityEngine.Object
	 *    or to make enumerators with specific names
	 * Create two classes that inherit from AbstractPairEnum and AbstractPair that 
	 *    set the concrete type of the value to make a new pair.
	 */
	public abstract class AbstractPairEnum<TValue, TPair> : AbstractEnum<TPair>
			where TPair : AbstractPair<TValue>{

		protected override string GetValueName(int index){
	        return values[index].name;
	    }
	}

	public abstract class AbstractPair<TValue>{
		public string name;
		public TValue value;
	}
}