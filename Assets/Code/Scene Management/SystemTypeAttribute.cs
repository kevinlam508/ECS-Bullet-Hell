using System.Collections;
using System.Collections.Generic;
using System;

[AttributeUsage(AttributeTargets.Class)]
public class SystemTypeAttribute : Attribute
{
	public ActiveSystemManager.SystemTypes Flag { get; private set; }

	public SystemTypeAttribute(ActiveSystemManager.SystemTypes flag){
		Flag = flag;
	}
}
