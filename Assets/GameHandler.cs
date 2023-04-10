using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDTK;
using UnityEngine;
using UnityEngine.UI;

public class GameHandler : MonoBehaviour
{
    public Transform circle1;
    public Transform circle2;

    public static int shift;
    public static int shift2;

    public static bool IsMovingOuter;
    public static bool IsMovingInner;

    private float cachedTimeScale = 1;

    public static int countOuter;
    public static int countInner;

    public static string txt;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.Space)) 
        {
            SetTimeScale();
        };
        // circle1.transform.Rotate(0, 0.05f, 0, Space.World);
        // circle2.transform.Rotate(0, 0.1f, 0, Space.World);
    }

    void SetTimeScale()
    {
        if (Time.timeScale != 0)
        {
            cachedTimeScale = Time.timeScale;
            Time.timeScale = 0;
        }
        else
        {
            Time.timeScale = cachedTimeScale;
        }
    }

    public static void CheckMovePos(int level, int change)
    {
        if(level == 0)
        {
            countOuter += change;
            if(countOuter == 0 && !IsMovingOuter)
            {
                GameObject.Find("OuterRotate").GetComponent<Button>().interactable = true;
            } 
            else
            {
                GameObject.Find("OuterRotate").GetComponent<Button>().interactable = false;
            }
        } 
        else
        {
            countInner += change;
            if (countInner == 0 && !IsMovingInner)
            {
                GameObject.Find("InnerRotate").GetComponent<Button>().interactable = true;
            }
            else
            {
                GameObject.Find("InnerRotate").GetComponent<Button>().interactable = false;
            }
        }
        // GameObject.Find("Testtest").GetComponentInChildren<Text>().text = countOuter + "/" + countInner;
    }

    public async void RotateLayer(int layer)
    {
        if (layer == 1)
        {
            if (IsMovingOuter) { return; }
            IsMovingOuter = true;
            CheckMovePos(0, 0);
            //GameObject.Find("Level1").GetComponent<Button>().interactable = false;

            var path1 = GameObject.Find("Path2C4").GetComponent<Path>();
            var path2 = GameObject.Find("Path2C19").GetComponent<Path>();
            //var path1 = GameObject.Find("Path1C12").GetComponent<Path>();
            //var path2 = GameObject.Find("Path1C27").GetComponent<Path>();

            if(path1.GetComponentInChildren<UnitCreep>() != null ||
                path2.GetComponentInChildren<UnitCreep>() != null)
            {
                IsMovingOuter = false;
                return;
            }

            path1.gameObject.SetActive(false);
            path2.gameObject.SetActive(false);

            var cyl = GameObject.Find("Cylinder00");

            //UnitCreep[] creeps = cyl.GetComponentsInChildren<UnitCreep>();
            //for(int i = 0; i < creeps.Length; i++)
            //{
            //    creeps[i].activeEffectMod.stun = true;
            //}
            // FindObjectOfType<UnitCreep>().activeEffectMod.stun = true;
            //FindObjectOfType<UnitCreep>().gameObject.transform.SetParent(GameObject.Find("Cylinder00").transform); // T

            // shift = shift == 1 ? 0 : 1;
            var angle = 0f;
            while (angle > -180)
            {
                angle -= 0.5f;
                circle1.transform.Rotate(0, -0.5f, 0, Space.World);
                await Task.Delay(1);
                // circle2.transform.Rotate(0, 0.1f, 0, Space.World);
            }


            path1.gameObject.SetActive(true);
            path2.gameObject.SetActive(true);

            //for (int i = 0; i < creeps.Length; i++)
            //{
            //    creeps[i].activeEffectMod.stun = false;
            //    creeps[i].NextWaypoint();
            //}
            var childs = cyl.GetComponentsInChildren<BuildPlatform>();
            for (int i = 0; i < childs.Length; i++)
            {
                childs[i].transform.SetParent(null);
                // childs[i].SetIsFormatted(false);
                childs[i].GenerateGraph(1);
                childs[i].transform.SetParent(cyl.transform);
            }

            await Task.Delay(500); //Task.Delay input is in milliseconds

            IsMovingOuter = false;
            CheckMovePos(0, 0);
            //GameObject.Find("Level1").GetComponent<Button>().interactable = true;
            //FindObjectOfType<UnitCreep>().activeEffectMod.stun = false;
            //FindObjectOfType<UnitCreep>().NextWaypoint();
        }
        else
        {
            if (IsMovingInner) { return; }
            IsMovingInner = true;
            CheckMovePos(1, 0);

            //var path1 = GameObject.Find("Path2C4").GetComponent<Path>();
            //var path2 = GameObject.Find("Path2C19").GetComponent<Path>();
            var path1 = GameObject.Find("Path3C12").GetComponent<Path>();
            var path2 = GameObject.Find("Path3C27").GetComponent<Path>();

            if (path1.GetComponentInChildren<UnitCreep>() != null ||
                path2.GetComponentInChildren<UnitCreep>() != null)
            {
                IsMovingInner = false;
                return;
            }

            path1.gameObject.SetActive(false);
            path2.gameObject.SetActive(false);

            //var path3 = GameObject.Find("Path30").GetComponent<Path>();
            //path3.connectorPoints[0] = path3.connectorPoints[0] == 26 ? 11 : 26;
            var cyl = GameObject.Find("Cylinder10");

            //shift2 = shift2 == 1 ? 0 : 1;

            //UnitCreep[] creeps = cyl.GetComponentsInChildren<UnitCreep>();
            //for (int i = 0; i < creeps.Length; i++)
            //{
            //    creeps[i].activeEffectMod.stun = true;
            //}

            //FindObjectOfType<UnitCreep>().activeEffectMod.stun = true;
            //FindObjectOfType<UnitCreep>().canBeTargeted = false;

            var angle = 0f;
            while (angle > -180)
            {
                angle -= 0.5f;
                circle2.transform.Rotate(0, -0.5f, 0, Space.World);
                await Task.Delay(1);
            }

            path1.gameObject.SetActive(true);
            path2.gameObject.SetActive(true);

            //for (int i = 0; i < creeps.Length; i++)
            //{
            //    creeps[i].activeEffectMod.stun = false;
            //    creeps[i].NextWaypoint();
            //}
            var childs = cyl.GetComponentsInChildren<BuildPlatform>();
            for (int i = 0; i < childs.Length; i++)
            {
                childs[i].transform.SetParent(null);
                // childs[i].SetIsFormatted(false);
                childs[i].GenerateGraph(1);
                childs[i].transform.SetParent(cyl.transform);
            }

            await Task.Delay(500); //Task.Delay input is in milliseconds

            IsMovingInner = false;
            CheckMovePos(1, 0);

            // path1.gameObject.GetComponentInChildren<LineRenderer>().gameObject.SetActive(true);
            //path2.gameObject.GetComponentInChildren<LineRenderer>().gameObject.SetActive(true);
        } 
    }


}
