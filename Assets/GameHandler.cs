using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDTK;
using UnityEngine;
using UnityEngine.UI;

public class GameHandler : MonoBehaviour
{
    public Transform circle0;
    public Transform circle1;
    public Transform circle2;

    public static int shift;
    public static int shift2;

    public static bool IsMovingOuter;
    public static bool IsMovingInner;
    public static bool IsMovingSmall;

    private float cachedTimeScale = 1;

    public static int countOuter;
    public static int countInner;

    public static string txt;

    // cached rotation UI; revert appears only after a rotation is made this turn
    private Button outerBtn, innerBtn, smallBtn, revertBtn;
    // which ring was rotated this turn, so revert knows what to reverse (-1 = none)
    private int lastRotatedLayer = -1;

    // Start is called before the first frame update
    void Start()
    {
        outerBtn = FindButton("OuterRotate");
        innerBtn = FindButton("InnerRotate");
        smallBtn = FindButton("SmallRotate");

        var revGO = GameObject.Find("RevertButton");
        if (revGO != null)
        {
            revertBtn = revGO.GetComponent<Button>();
            if (revertBtn != null) revertBtn.onClick.AddListener(RevertRotation);
            revGO.SetActive(false);
        }
    }

    private Button FindButton(string n)
    {
        var go = GameObject.Find(n);
        return go != null ? go.GetComponent<Button>() : null;
    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.Space)) 
        {
            SetTimeScale();
        };

        RefreshRotationUI();
        UpdateCreepHover();
    }

    // shows the hovered creep's unitName in the shared tooltip; only hides it when we were the one showing it
    private bool creepTooltipShown = false;
    private void UpdateCreepHover()
    {
        var cam = Camera.main;
        if (cam == null) return;

        bool overUI = UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (!overUI)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            UnitCreep nearest = null;
            float best = Mathf.Infinity;
            foreach (var h in Physics.RaycastAll(ray))
            {
                var c = h.collider.GetComponentInParent<UnitCreep>();
                if (c != null && !c.IsDestroyed() && h.distance < best) { best = h.distance; nearest = c; }
            }
            if (nearest != null)
            {
                UITooltip.ShowCreepName(nearest.unitName, Input.mousePosition + new Vector3(14, 14, 0));
                creepTooltipShown = true;
                return;
            }
        }

        if (creepTooltipShown) { UITooltip.Hide(); creepTooltipShown = false; }
    }

    // rotation buttons are usable only when the turn manager allows it (planning, off cooldown,
    // not already rotated this turn) and the ring is clear of creeps; revert shows after a rotation
    private void RefreshRotationUI()
    {
        var tm = TurnManager.GetInstance();

        if (outerBtn != null) outerBtn.interactable = tm != null && tm.CanRotateRing(0) && !IsMovingOuter && countOuter == 0;
        if (innerBtn != null) innerBtn.interactable = tm != null && tm.CanRotateRing(1) && !IsMovingInner && countInner == 0;
        if (smallBtn != null) smallBtn.interactable = tm != null && tm.CanRotateRing(2) && !IsMovingSmall && countInner == 0;

        if (revertBtn != null)
        {
            bool show = tm != null && tm.CanRevertRingRotation();
            if (revertBtn.gameObject.activeSelf != show) revertBtn.gameObject.SetActive(show);
        }
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
        else if (level == 1)
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
        else if (level == 2)
        {
            countInner += change;
            if (countInner == 0 && !IsMovingSmall)
            {
                GameObject.Find("SmallRotate").GetComponent<Button>().interactable = true;
            }
            else
            {
                GameObject.Find("SmallRotate").GetComponent<Button>().interactable = false;
            }
        }
        // GameObject.Find("Testtest").GetComponentInChildren<Text>().text = countOuter + "/" + countInner;
    }

    public async void RotateLayer(int layer)
    {
        var tm = TurnManager.GetInstance();
        if (tm == null || !tm.CanRotateRing(layer)) return;

        bool ok = await DoRotate(layer, false);
        if (ok)
        {
            lastRotatedLayer = layer;
            tm.NotifyRingRotated(layer);
        }
    }

    // reverse this turn's rotation so it's as if nothing happened (no cooldown committed)
    public async void RevertRotation()
    {
        var tm = TurnManager.GetInstance();
        if (tm == null || !tm.CanRevertRingRotation() || lastRotatedLayer < 0) return;

        int layer = lastRotatedLayer;
        bool ok = await DoRotate(layer, true);
        if (ok)
        {
            lastRotatedLayer = -1;
            tm.OnRingRotationReverted();
        }
    }

    // shared rotation for all rings; revert=true spins back by the same angle
    private async Task<bool> DoRotate(int layer, bool revert)
    {
        Transform circle; string p1n, p2n; float mag; int level;
        switch (layer)
        {
            case 0: circle = circle0; p1n = "Path1C12"; p2n = "Path1C27"; mag = 120f; level = 0; break;
            case 1: circle = circle1; p1n = "Path2C4"; p2n = "Path2C19"; mag = 120f; level = 1; break;
            default: circle = circle2; p1n = "Path3C12"; p2n = "Path3C27"; mag = 180f; level = 2; break;
        }

        if (IsLayerMoving(layer)) return false;
        SetLayerMoving(layer, true);
        CheckMovePos(level, 0);

        var path1 = GameObject.Find(p1n).GetComponent<Path>();
        var path2 = GameObject.Find(p2n).GetComponent<Path>();

        if (path1.GetComponentInChildren<UnitCreep>() != null ||
            path2.GetComponentInChildren<UnitCreep>() != null)
        {
            SetLayerMoving(layer, false);
            return false;
        }

        path1.gameObject.SetActive(false);
        path2.gameObject.SetActive(false);

        float total = revert ? mag : -mag;   // forward rotates negative, revert positive
        float step = 0.5f * Mathf.Sign(total);
        float rotated = 0f;
        while (Mathf.Abs(rotated) < Mathf.Abs(total))
        {
            circle.Rotate(0, step, 0, Space.World);
            rotated += step;
            await Task.Delay(1);
        }

        path1.gameObject.SetActive(true);
        path2.gameObject.SetActive(true);

        var childs = circle.gameObject.GetComponentsInChildren<BuildPlatform>();
        for (int i = 0; i < childs.Length; i++)
        {
            childs[i].transform.SetParent(null);
            childs[i].GenerateGraph(1);
            childs[i].transform.SetParent(circle.transform);
        }

        await Task.Delay(500); //Task.Delay input is in milliseconds

        SetLayerMoving(layer, false);
        CheckMovePos(level, 0);
        return true;
    }

    private bool IsLayerMoving(int layer)
    {
        return layer == 0 ? IsMovingOuter : layer == 1 ? IsMovingInner : IsMovingSmall;
    }

    private void SetLayerMoving(int layer, bool v)
    {
        if (layer == 0) IsMovingOuter = v;
        else if (layer == 1) IsMovingInner = v;
        else IsMovingSmall = v;
    }


}
