using System.Collections;
using System.Collections.Generic;
using TDTK;
using UnityEngine;
using UnityEngine.UI;

// Turn-based phase controller layered on top of TDTK.
// Planning: grant income, spawn this turn's wave, build/rotate, show movement preview.
// Resolution: creeps advance to their precomputed next-round point while towers fire (real-time).
// When resolution settles, a new turn begins (or the game ends).
public class TurnManager : MonoBehaviour
{
    public enum Phase { Planning, Resolution, GameOver }

    public static Phase phase = Phase.Planning;
    public static int turnNumber = 0;

    [Header("Economy")]
    // gold granted at the start of each planning turn; default used when no per-turn override is set
    public int defaultIncomePerTurn = 20;
    // optional per-turn overrides; index 0 = turn 1, index 1 = turn 2, ...
    public List<int> incomePerTurnOverrides = new List<int>();

    [Header("Movement")]
    // seconds of real-time travel a creep is granted per turn; distance/round = speed * this
    public float secondsPerTurn = 6f;

    [Header("Rings")]
    public int ringCount = 3;
    // turns a rotation is locked out after use; 3 => usable again on the 3rd turn (once per 3 turns)
    public int ringRechargeTurns = 3;

    // shared cooldown across all rings (in turns); 0 means a rotation is available
    private int ringCooldown = 0;
    // which ring was rotated this turn (-1 = none); enforces one rotation per turn
    private int ringRotatedThisTurn = -1;

    public static bool IsPlanning() { return phase == Phase.Planning; }
    public static bool IsResolving() { return phase == Phase.Resolution; }

    // gate checked by creep movement; creeps only advance during resolution
    public static bool CanCreepsMove() { return phase == Phase.Resolution; }

    // distance a creep may travel this turn, derived from its current speed
    public static float GetCreepDistanceBudget(UnitCreep creep)
    {
        float seconds = instance != null ? instance.secondsPerTurn : 2f;
        return creep.GetSpeed() * seconds;
    }

    private static TurnManager instance;
    public static TurnManager GetInstance() { return instance; }

    // optional End Turn button, located by name at startup
    public Button endTurnButton;

    void Awake()
    {
        instance = this;
        ringCooldown = 0;
    }

    void Start()
    {
        turnNumber = 0;
        if (endTurnButton == null)
        {
            GameObject btnObj = GameObject.Find("EndTurnButton");
            if (btnObj != null) endTurnButton = btnObj.GetComponent<Button>();
        }
        if (endTurnButton != null) endTurnButton.onClick.AddListener(EndTurn);
        StartCoroutine(BeginFirstTurn());
    }

    // wait until SpawnManager.Start and PerkManager's deferred resource-list init have run
    private IEnumerator BeginFirstTurn()
    {
        yield return null;
        while (PerkManager.rscGainModList == null || PerkManager.rscGainModList.Count == 0)
            yield return null;
        BeginTurn();
    }

    void Update()
    {
        if (phase != Phase.Resolution) return;

        if (ResolutionComplete())
        {
            if (GameControl.IsGameOver()) { phase = Phase.GameOver; return; }
            BeginTurn();
        }
    }

    // ----- Phase transitions -----

    private void BeginTurn()
    {
        phase = Phase.Planning;
        turnNumber += 1;
        ringRotatedThisTurn = -1;

        TickCooldowns();
        GrantIncome();
        SpawnWaveForTurn();
        RecomputePreviews();

        // show the End Turn button only while the player is planning
        if (endTurnButton != null) endTurnButton.gameObject.SetActive(true);
    }

    // hooked to the "End Turn" button
    public void EndTurn()
    {
        if (phase != Phase.Planning) return;
        // hide the button during the creep (resolution) turn
        if (endTurnButton != null) endTurnButton.gameObject.SetActive(false);
        LockPlanningActions();
        HideAllGhosts();
        phase = Phase.Resolution;
    }

    // ----- Economy -----

    private void GrantIncome()
    {
        int amount = GetIncomeForTurn(turnNumber);
        if (amount == 0) return;
        RscManager.GainRsc(new List<int> { amount });
    }

    // per-turn income: override list (index 0 = turn 1) if present, else the default
    private int GetIncomeForTurn(int turn)
    {
        int idx = turn - 1;
        if (idx >= 0 && idx < incomePerTurnOverrides.Count) return incomePerTurnOverrides[idx];
        return defaultIncomePerTurn;
    }

    // ----- Spawning -----

    // place this turn's wave instantly at spawn points (not trickled over time)
    private void SpawnWaveForTurn()
    {
        SpawnManager.SpawnWaveInstant();
    }

    // ----- Rings (one rotation per turn, revertible same turn, shared recharge in turns) -----

    private void TickCooldowns()
    {
        ringCooldown = Mathf.Max(0, ringCooldown - 1);
    }

    public bool CanRotateRing(int ring)
    {
        if (phase != Phase.Planning) return false;
        if (ringRotatedThisTurn != -1) return false;
        if (ring < 0 || ring >= ringCount) return false;
        return ringCooldown == 0;
    }

    // called by GameHandler after a successful rotation
    public void NotifyRingRotated(int ring)
    {
        ringRotatedThisTurn = ring;
        RecomputePreviews();
    }

    // allow undoing this turn's rotation before ending the turn
    public bool CanRevertRingRotation() { return phase == Phase.Planning && ringRotatedThisTurn != -1; }

    // GameHandler performs the actual reverse rotation; this clears the per-turn flag
    public void OnRingRotationReverted()
    {
        ringRotatedThisTurn = -1;
        RecomputePreviews();
    }

    // ----- Resolution / preview seams -----

    // resolution ends when every living creep has spent its movement budget (parked) or none remain
    private bool ResolutionComplete()
    {
        List<Unit> list = SpawnManager.GetActiveUnitList();
        if (list.Count == 0) return true;
        for (int i = 0; i < list.Count; i++)
        {
            UnitCreep creep = list[i].GetCreep();
            if (creep == null) continue;
            if (!creep.IsParkedThisTurn()) return false;
        }
        return true;
    }

    // commit the shared rotation cooldown when the turn ends (only if a rotation was kept)
    private void LockPlanningActions()
    {
        if (ringRotatedThisTurn != -1)
            ringCooldown = ringRechargeTurns;
    }

    // recompute each active creep's next-round stop point and show its ghost preview
    private void RecomputePreviews()
    {
        List<Unit> list = SpawnManager.GetActiveUnitList();
        for (int i = 0; i < list.Count; i++)
        {
            UnitCreep creep = list[i].GetCreep();
            if (creep == null) continue;
            Vector3 dest = creep.SimulateTurnDestination();
            creep.ShowPreviewGhost(dest);
        }
    }

    // remove all ghost previews (called when resolution starts)
    private void HideAllGhosts()
    {
        List<Unit> list = SpawnManager.GetActiveUnitList();
        for (int i = 0; i < list.Count; i++)
        {
            UnitCreep creep = list[i].GetCreep();
            if (creep == null) continue;
            creep.HidePreviewGhost();
        }
    }
}
