using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;					// Editor, AssetDatabase
using System;						// Array

namespace EditableEnum{
    [CustomEditor(typeof(AbstractEnum), true)]
    [CanEditMultipleObjects]
    public class AbstractEnumEditor : Editor{
        
        private enum Amount{None, Some, All}
        private static string[] buttonText = {"Update", "Update and Generate", "Generate"};

        public override void OnInspectorGUI(){
        	
        	AbstractEnum[] enums = Array.ConvertAll(serializedObject.targetObjects,
        		(UnityEngine.Object o) => { return (AbstractEnum)o; } );

            // buttom setup
            Amount genAmount = GenerateAmount(enums);
        	if(GUILayout.Button(buttonText[(int)genAmount])){
                foreach(AbstractEnum e in enums){
                	e.UpdateEnumerator();
                }
            }

            DrawDefaultInspector();
        }

        /*
         * returns how many of the enums will generate files on update
         */
        private static Amount GenerateAmount(AbstractEnum[] enums){
            // determine buttom text
            int updateCount = 0;
            foreach(AbstractEnum e in enums){
                if(e.WillOverwrite){
                    ++updateCount;
                }
            }
            if(updateCount == enums.Length){
                return Amount.None;
            }
            else if(updateCount == 0){
                return Amount.All;
            }
            else{
                return Amount.Some;
            }
        }
    }
}