using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
public class Notes : MonoBehaviour
{
	[TextArea(20, 30)]
    public string notes;
}
#endif
