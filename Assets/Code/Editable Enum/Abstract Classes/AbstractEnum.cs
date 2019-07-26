using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;					// Editor, AssetDatabase

using System;						// Serializable
using System.IO;					// StreamWriter
using System.Globalization;			// UnityCategory

/*
 * Base class for all Editable Enums.
 * Handles creation of the enumerator and storing parameters.
 * Child classes only need to define T, the type to list as values, and 
 *    GetValueName(), which gives the name of the ith value. The child class 
 *    will also need SerializeAttribute and MenuItemAttribute.
 */
namespace EditableEnum{

    // Abstract super class so the custom editor can edit all child classes
    public abstract class AbstractEnum : ScriptableObject{
        public abstract bool WillOverwrite{
            get;
        }
        public abstract void UpdateEnumerator();
    }

    public abstract class AbstractEnum<TValue> : AbstractEnum{
        public enum AccessType { None, Public, Protected, Internal, ProtectedInternal, Private, PrivateProtected}
    	private static string[] accessStrings = {
    		"",
    		"public ",
    		"protected ",
    		"internal ",
    		"protected internal ",
    		"private ",
    		"private protected "
    	};

    	[Tooltip("The name of the file to put the enumerator in."
        	+ "\nThe file will be completely overriden and will be placed in the same folder as this object.")]
    	public string fileName;

        [Tooltip("The namespace to put the enumerator in."
        	+ "\nLeave blank to not have a namespace.")]
        public string namespaceName;

        [Header("Class Parameters")]
        [Tooltip("The class to put the enumerator in."
        	+ "\nLeave blank to not have a namespace.")]
        public string className;
        [Tooltip("Is the class a partial class if listed")]
        public bool isPartial;
        [Tooltip("Access modifier of the class if listed. Can only be none, public, or internal.")]
        public AccessType classAccessType;

        [Header("Enumerator Parameters")]
        [Tooltip("The name of the enumerator.")]
        public string enumeratorName;
        [Tooltip("Access modifier of the enumerator if listed. Can only be none, public, or internal if not in a class")]
        public AccessType enumAccessType;
        [Tooltip("The values in order.")]
        public List<TValue> values;

        /*
         * returns the file path for the generated file
         */
        public virtual string GeneratedFilePath{ 
        	get{
    	    	string origPath = AssetDatabase.GetAssetPath(this);
    	    	int idx = origPath.LastIndexOf('/');

    	    	// not created yet
    	    	if(idx < 0){
    	    		return null;
    	    	}

    	    	string directory = origPath.Substring(0, idx);
    	    	string path = directory + "\\" + fileName + ".cs";
    	    	return path;
        	}
        }

        public override bool WillOverwrite => File.Exists(GeneratedFilePath);

        /*
         * Updates or creates the file for the enumerator, will override the file
         */
        public override void UpdateEnumerator(){
        	if(IsValid()){
    	    	string path = GeneratedFilePath;
    	    	string code = GenerateCode();
    	    	using (StreamWriter outfile = new StreamWriter(path))
                {
                	outfile.Write(code);
                }
                Debug.Log("Wrote file " + path);
                AssetDatabase.Refresh();
        	}
        }

        public bool HasNamespace => namespaceName.Length > 0;
        public bool HasClass => className.Length > 0;

        /*
         * returns the C# code for the enumerator
         */
        protected virtual string GenerateCode(){
    		string code = "";
        	string tabs = "";
        	// open namespace
        	if(HasNamespace){
        		code += "namespace " + namespaceName + "{\n";
        		tabs += "\t";
        	}

    		// open class
        	if(HasClass){
        		code += tabs + accessStrings[(int)classAccessType] 
        			+ ((isPartial) ? "partial " : "") + "class " + className + "{\n";
        		tabs += "\t";
        	}

        	// open enum
        	code += tabs + accessStrings[(int)enumAccessType] + "enum " + enumeratorName + "{\n";
        	tabs += "\t";

        	// list values
        	for(int i = 0; i < values.Count - 1; ++i){
        		code += tabs + GetValueName(i) + ",\n";
        	}
        	code += tabs + GetValueName(values.Count - 1) + "\n";

        	// close enum
        	tabs = tabs.Remove(tabs.Length - 1);
        	code += tabs + "}\n";

    		// close class
        	if(HasClass){
        		tabs = tabs.Remove(tabs.Length - 1);
        		code += tabs + "}\n";
        	}

        	// close namespace
        	if(HasNamespace){
        		code += "}\n";
        		tabs = tabs.Remove(tabs.Length - 1);
        	}

        	return code;
        }

        protected abstract string GetValueName(int index);

        /*
         * returns true if the type is public
         */
        public bool IsPublicAccess(AccessType type){
        	return type == AccessType.None || type == AccessType.Public || type == AccessType.Internal;
        }

        /*
         * returns true if the enumerator can be made
         */
        public virtual bool IsValid(){
        	bool res = true;
        	// check file name
    		if(fileName.Length == 0 && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0){
        		res = false;
                Debug.LogError("File Name \"" + fileName + "\" is not a valid name");
        	}

        	// check namespace name and levels
        	if(HasNamespace){
        		bool nameValid = true;
        		foreach(string s in namespaceName.Split('.')){
        			if(!IsValidIdentifier(s)){
        				nameValid = false;
        			}
        		}
        		if(!nameValid){
    	    		res = false;
    	    		Debug.LogError("Namespace Name \"" + namespaceName + "\" is not a valid name");
    	    	}
        	}

        	// check class vars
        	if(HasClass && !IsValidIdentifier(className)){
        		res = false;
        		Debug.LogError("Class Name \"" + className + "\" is not a valid name");
        	}
        	// class must be public or internal
        	if(!IsPublicAccess(classAccessType)){
        		res = false;
        		Debug.LogError("Class can only be none, public, or internal access");
        	}

        	// check enumerator vars
        	if(!IsValidIdentifier(enumeratorName)){
        		res = false;
        		Debug.LogError("Enumerator Name \"" + enumeratorName + "\" is not a valid name");
        	}
        	if(!HasClass && !IsPublicAccess(enumAccessType)){
        		res = false;
        		Debug.LogError("Enum not in a class can only be none, public, or internal access");
        	}

        	// check values
        	if(values.Count == 0){
        		res = false;
        		Debug.LogError("There are no value names");
        	}
        	for(int i = 0; i < values.Count; ++i){
        		string val = GetValueName(i);
    	    	if(!IsValidIdentifier(val)){
    	    		res = false;
    	    		Debug.LogError("Value Name " + i + " \"" + val + "\" is not a valid name");
    	    	}
        	}
        	for(int i = 0; i < values.Count; ++i){
        		for(int j = i + 1; j < values.Count; ++j){
        			if(GetValueName(i) == GetValueName(j)){
        				res = false;
        				Debug.LogError("Value names " + i + " and " + j + " both are " + GetValueName(i));
        			}
        		}
        	}
        	return res;
        }

        private static string[] keywords = {
        	"abstract" , "as"       , "base"       , "bool"      , "break",
        	"byte"     , "case"     , "catch"      , "char"      , "checked",
        	"class"    , "const"    , "continue"   , "decimal"   , "default",
        	"delegate" , "do"       , "double"     , "else"      , "enum",
        	"event"    , "explicit" , "extern"     , "false"     , "finally",
        	"fixed"    , "float"    , "for"        , "foreach"   , "goto",
        	"if"       , "implicit" , "in"         , "int"       , "interface",
        	"internal" , "is"       , "lock"       , "long"      , "namespace",
        	"new"      , "null"     , "object"     , "operator"  , "out",
        	"override" , "params"   , "private"    , "protected" , "public",
        	"readonly" , "ref"      , "return"     , "sbyte"     , "sealed",
        	"short"    , "sizeof"   , "stackalloc" , "static"    , "string",
        	"struct"   , "switch"   , "this"       , "throw"     , "true",
        	"try"      , "typeof"   , "uint"       , "ulong"     , "unchecked",
        	"unsafe"   , "ushort"   , "using"      , "virtual"   , "void",
        	"volatile" , "while"
        };

        /*
         * returns true if a given string follows C# standard for identifiers
         */
        private static bool IsValidIdentifier(string identifier){
        	if(identifier.Length == 0){
        		return false;
        	}

        	// identifier can be @ followed by a keyword
        	if(identifier[0] == '@' && Array.Exists(keywords, identifier.Substring(1).Equals)){
        		return true;
        	}

        	// general identifier rules
        	if (!char.IsLetter(identifier[0]) && identifier[0] != '_'){
    	        return false;
    	    }
    	    for (int i = 1; i < identifier.Length; ++i){
    	        if (!ValidMidChar(identifier[i])){
    	           return false;
    	        }
    	    }
    	    return true;
        }

        /*
         * returns true if c can be used in the middle of a C# identifier
         */
        private static bool ValidMidChar(char c){
        	UnicodeCategory cat = char.GetUnicodeCategory(c);
        	return char.IsLetterOrDigit(c) || cat == UnicodeCategory.NonSpacingMark 
        		|| cat == UnicodeCategory.SpacingCombiningMark 
        		|| cat == UnicodeCategory.ConnectorPunctuation 
        		|| cat == UnicodeCategory.Format; 
        }
    }
}