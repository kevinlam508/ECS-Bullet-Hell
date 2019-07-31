using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 *  Interface to tell ActiveSystemManager to reload this system when active
 *     in a different scene
 */
public interface IHasSceneData
{
	void ReloadData();
}
