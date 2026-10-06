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

    // bridges kept on one side only: outer=upper, middle=lower, inner=upper. The opposite-side
    // bridge of each ring is disabled at startup so creeps always cross on the chosen side.
    private static readonly string[] allBridges = { "Path1C12", "Path1C27", "Path2C4", "Path2C19", "Path3C12", "Path3C27" };
    private static readonly string[] disabledBridges = { "Path1C27", "Path2C4", "Path3C27" };
    private readonly Dictionary<string, GameObject> bridgeCache = new Dictionary<string, GameObject>();

    public static bool IsMovingOuter;
    public static bool IsMovingInner;
    public static bool IsMovingSmall;

    private float cachedTimeScale = 1;

    public static string txt;

    // cached rotation UI; revert appears only after a rotation is made this turn
    private Button outerBtn, innerBtn, smallBtn, revertBtn;
    // which ring was rotated this turn, so revert knows what to reverse (-1 = none)
    private int lastRotatedLayer = -1;

    // Start is called before the first frame update
    void Start()
    {
        // turn-based game: no need to render faster than this, keeps the GPU/CPU idle
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

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

        ConfigureBridges();
    }

    // cache every bridge (while still active) and disable the opposite-side bridge of each ring
    private void ConfigureBridges()
    {
        for (int i = 0; i < allBridges.Length; i++)
        {
            var go = GameObject.Find(allBridges[i]);
            if (go != null) bridgeCache[allBridges[i]] = go;
        }
        for (int i = 0; i < disabledBridges.Length; i++)
        {
            GameObject go;
            if (bridgeCache.TryGetValue(disabledBridges[i], out go) && go != null) go.SetActive(false);
        }
    }

    // look up a bridge even when inactive (GameObject.Find skips inactive objects)
    private GameObject GetBridge(string n)
    {
        GameObject go;
        if (bridgeCache.TryGetValue(n, out go) && go != null) return go;
        return GameObject.Find(n);
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
    }

    // rotation buttons are usable only when the turn manager allows it (planning, off cooldown,
    // not already rotated this turn) and the ring is clear of creeps; revert shows after a rotation
    private void RefreshRotationUI()
    {
        var tm = TurnManager.GetInstance();

        // rotation is allowed even with creeps on the ring; they get carried around during the spin
        if (outerBtn != null) outerBtn.interactable = tm != null && tm.CanRotateRing(0) && !IsMovingOuter;
        if (innerBtn != null) innerBtn.interactable = tm != null && tm.CanRotateRing(1) && !IsMovingInner;
        if (smallBtn != null) smallBtn.interactable = tm != null && tm.CanRotateRing(2) && !IsMovingSmall;

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
        Transform circle; string p1n, p2n; float mag;
        switch (layer)
        {
            // outer ring platforms live under an unscaled pivot (CylinderOuter has non-uniform scale and would distort them)
            case 0: circle = GameObject.Find("OuterRingPivot").transform; p1n = "Path1C12"; p2n = "Path1C27"; mag = 120f; break;
            case 1: circle = circle1; p1n = "Path2C4"; p2n = "Path2C19"; mag = 120f; break;
            default: circle = circle2; p1n = "Path3C12"; p2n = "Path3C27"; mag = 180f; break;
        }

        if (IsLayerMoving(layer)) return false;
        SetLayerMoving(layer, true);

        var b1 = GetBridge(p1n);
        var b2 = GetBridge(p2n);

        // carry any creeps on this ring so they rotate along with it, then restore their parent afterwards.
        // detach to the scene root (uniform) and spin them via RotateAround so a scaled ring never distorts them
        var ridingCreeps = new List<UnitCreep>();
        if (b1 != null) ridingCreeps.AddRange(b1.GetComponentsInChildren<UnitCreep>());
        if (b2 != null) ridingCreeps.AddRange(b2.GetComponentsInChildren<UnitCreep>());
        var creepParents = new List<Transform>();
        foreach (var c in ridingCreeps)
        {
            creepParents.Add(c.transform.parent);
            c.transform.SetParent(null, true);
        }

        // remember each bridge's state so a disabled (one-side) bridge is not re-enabled after the spin
        bool b1Active = b1 != null && b1.activeSelf;
        bool b2Active = b2 != null && b2.activeSelf;
        if (b1 != null) b1.SetActive(false);
        if (b2 != null) b2.SetActive(false);

        float total = revert ? mag : -mag;   // forward rotates negative, revert positive
        float step = 0.5f * Mathf.Sign(total);
        float rotated = 0f;
        while (Mathf.Abs(rotated) < Mathf.Abs(total))
        {
            circle.Rotate(0, step, 0, Space.World);
            foreach (var c in ridingCreeps) if (c != null) c.transform.RotateAround(Vector3.zero, Vector3.up, step);
            rotated += step;
            await Task.Delay(1);
        }

        if (b1 != null) b1.SetActive(b1Active);
        if (b2 != null) b2.SetActive(b2Active);

        for (int i = 0; i < ridingCreeps.Count; i++)
        {
            if (ridingCreeps[i] != null) ridingCreeps[i].transform.SetParent(creepParents[i], true);
        }

        var childs = circle.gameObject.GetComponentsInChildren<BuildPlatform>();
        for (int i = 0; i < childs.Length; i++)
        {
            childs[i].transform.SetParent(null);
            childs[i].GenerateGraph(1);
            childs[i].transform.SetParent(circle.transform);
        }

        await Task.Delay(500); //Task.Delay input is in milliseconds

        SetLayerMoving(layer, false);
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
