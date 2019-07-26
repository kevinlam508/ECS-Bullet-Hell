using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;					// Editor, AssetDatabase

using System;						// Serializable
using System.IO;					// StreamWriter
using System.Globalization;			// UnityCategory

namespace EditableEnum{
    /*
     * Class to have an enumerator for any Unity.Object.
     * Uses the name of the object after removing whitespace for the value names.
     */
    public abstract class AbstractUnityObjectEnum<TValue> : AbstractEnum<TValue>
            where TValue : UnityEngine.Object{

        protected override string GetValueName(int index){
            return values[index].name.Replace(" ", "");
        }
    }
}
