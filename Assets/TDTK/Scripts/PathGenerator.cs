using System.Collections.Generic;
using System.Linq;
using TDTK;
using UnityEditor;
using UnityEngine;

class PathGenerator
{
    public static Path path;

    [MenuItem("Window/Generate Path")]
    static void GenerateBasePath()
    {
        GenerateBasePathExec();
    }

    static List<GameObject> GenerateBasePathExec()
    {
        if (!path)
        {
            path = GameObject.Find("Path02").GetComponent<Path>();
            for(int i = 0; i < path.transform.childCount; i++)
            {
                path.transform.GetChild(i).transform.position = new Vector3(path.transform.GetChild(i).transform.position.x -1 ,
                    path.transform.GetChild(i).transform.position.y, path.transform.GetChild(i).transform.position.z);
            }
            //path.waypointTList = new();
        }
        List<GameObject> wayipoints = new ();

        var waypoint = Resources.Load("Waypoint");

        var degrees = 0;

        var xCurr = 8;
        var zCurr = 4;

        //while (zCurr > -3)
        //{
        //    zCurr -= 1;
        //    GameObject wayipointInstance = GameObject.Instantiate(waypoint) as GameObject;
        //    wayipointInstance.transform.position = new Vector3(xCurr, 0, zCurr);
        //    wayipointInstance.transform.SetParent(path.transform);
        //    wayipoints.Add(wayipointInstance);
        //    path.waypointTList.Add(wayipointInstance.transform);
        //}
        //while (xCurr > -8)
        //{
        //    xCurr -= 1;
        //    GameObject wayipointInstance = GameObject.Instantiate(waypoint) as GameObject;
        //    wayipointInstance.transform.position = new Vector3(xCurr, 0, zCurr);
        //    wayipointInstance.transform.SetParent(path.transform);
        //    wayipoints.Add(wayipointInstance);
        //    path.waypointTList.Add(wayipointInstance.transform);
        //}
        //while (zCurr < 3)
        //{
        //    zCurr += 1;
        //    GameObject wayipointInstance = GameObject.Instantiate(waypoint) as GameObject;
        //    wayipointInstance.transform.position = new Vector3(xCurr, 0, zCurr);
        //    wayipointInstance.transform.SetParent(path.transform);
        //    wayipoints.Add(wayipointInstance);
        //    path.waypointTList.Add(wayipointInstance.transform);
        //}
        //while (xCurr < 8)
        //{
        //    xCurr += 1;
        //    GameObject wayipointInstance = GameObject.Instantiate(waypoint) as GameObject;
        //    wayipointInstance.transform.position = new Vector3(xCurr, 0, zCurr);
        //    wayipointInstance.transform.SetParent(path.transform);
        //    wayipoints.Add(wayipointInstance);
        //    path.waypointTList.Add(wayipointInstance.transform);
        //}


        //try
        //{
        //    var fileData = System.IO.File.ReadAllText(Application.dataPath + "/p3.csv");
        //    var lines = fileData.Split("\n"[0]);
        //    for (int j = 0; j < lines.Length; j++)
        //    {
        //        var lineData = (lines[j].Trim()).Split(","[0]);

        //        GameObject wayipointInstance = GameObject.Instantiate(waypoint) as GameObject;
        //        wayipointInstance.transform.position = new Vector3(float.Parse(lineData[0]), 0, float.Parse(lineData[1]));
        //        wayipointInstance.transform.SetParent(path.transform);
        //        wayipoints.Add(wayipointInstance);

        //        path.waypointTList.Add(wayipointInstance.transform);


        //        //}
        //    }
        //}
        //catch { }

        //while (degrees <= 360)
        //{
        //    degrees += 12;
        //    var radians = degrees * Mathf.Deg2Rad;
        //    var x = Mathf.Cos(radians) * 13;
        //    var z = Mathf.Sin(radians) * 13;

        //    GameObject wayipointInstance = GameObject.Instantiate(waypoint) as GameObject;
        //    wayipointInstance.transform.position = new Vector3(x, 0, z);
        //    wayipointInstance.transform.SetParent(path.transform);
        //    wayipoints.Add(wayipointInstance);

        //    path.waypointTList.Add(wayipointInstance.transform);
        //}

        return wayipoints;
    }
}