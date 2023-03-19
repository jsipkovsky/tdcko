using System.Collections;
using System.Collections.Generic;
using TDTK;
using UnityEngine;

public class SetParents : MonoBehaviour
{
    public Transform parent;
    // Start is called before the first frame update
    void Start()
    {
        var childs = GetComponentsInChildren<BuildPlatform>();
        for (int i = 0; i < childs.Length; i++){
            childs[i].gameObject.transform.SetParent(parent);
        }
    }
}
