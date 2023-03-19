using System.Collections;
using System.Collections.Generic;
using TDTK;
using UnityEngine;

public class TestPaths : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Change()
    {
        var unit = GameObject.FindObjectOfType<UnitCreep>();
        unit.path = GameObject.Find("Path2").GetComponent<Path>();
    }
}
