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

    // set once the final wave is out: creeps then move with no per-turn budget so their walk to the
    // end is one smooth run instead of stopping and resuming in secondsPerTurn-long segments
    public static bool continuous = false;

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

    // distance a creep may travel this turn, derived from its current speed;
    // in continuous mode the budget is unbounded so creeps never park mid-walk
    public static float GetCreepDistanceBudget(UnitCreep creep)
    {
        if (continuous) return float.PositiveInfinity;
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
        continuous = false;
        if (endTurnButton == null)
        {
            GameObject btnObj = GameObject.Find("EndTurnButton");
            if (btnObj != null) endTurnButton = btnObj.GetComponent<Button>();
        }
        if (endTurnButton != null) endTurnButton.onClick.AddListener(EndTurn);

        // clear any upgrades picked in a previous play session (static state survives domain reloads)
        UpgradeState.Reset();

        // create the modifier popup manager on the fly so no scene wiring is needed
        if (ModifierManager.GetInstance() == null)
            new GameObject("ModifierManager").AddComponent<ModifierManager>();

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

        // drives time-based upgrade effects (e.g. Blade "Tough shift" recovery windows)
        UpgradeState.ResolutionTime += Time.deltaTime;

        if (ResolutionComplete())
        {
            if (GameControl.IsGameOver()) { phase = Phase.GameOver; return; }

            // once the last wave is out there are no more planning turns: creeps just keep walking
            // (continuous mode) until they all die or reach the end, with no End Turn button
            if (!SpawnManager.HasPendingSpawns())
            {
                // end when the board is clear, or when every remaining creep is wedged at a dead-end
                // (no ring rotation is available to clear it) so it can never progress or leak
                if (!AnyCreepsRemain() || AllRemainingCreepsStuck())
                {
                    GameControl.EndGame();
                    phase = Phase.GameOver;
                    return;
                }
                // hand out an unlimited budget from here on so the remaining walk is one smooth run
                continuous = true;
                PrepareCreepsForResolution();
                return;
            }

            BeginTurn();
        }
    }

    // true if any creep is still alive on the board
    private static bool AnyCreepsRemain()
    {
        List<Unit> list = SpawnManager.GetActiveUnitList();
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].GetCreep() != null) return true;
        return false;
    }

    // true when at least one creep remains and all remaining creeps are stuck at a dead-end;
    // used as a continuous-mode fail-safe so a wedged board can't hang the game forever
    private static bool AllRemainingCreepsStuck()
    {
        List<Unit> list = SpawnManager.GetActiveUnitList();
        bool any = false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null) continue;
            UnitCreep creep = list[i].GetCreep();
            if (creep == null) continue;
            any = true;
            if (!creep.IsStuckThisTurn()) return false;
        }
        return any;
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

        // every 3 waves (turns 4, 7, 10, ...) offer a modifier choice before the player acts
        ModifierManager.OfferIfDue(turnNumber, this);
    }

    // hide/show the End Turn button (used while the modifier popup blocks planning)
    public void SetEndTurnButtonVisible(bool visible)
    {
        if (endTurnButton != null) endTurnButton.gameObject.SetActive(visible);
    }

    // hooked to the "End Turn" button
    public void EndTurn()
    {
        if (phase != Phase.Planning) return;
        // can't end the turn while the modifier choice popup is open
        if (ModifierManager.IsBlocking()) return;
        // hide the button during the creep (resolution) turn
        if (endTurnButton != null) endTurnButton.gameObject.SetActive(false);
        UpgradeState.OnPlayerTurnEnd();
        LockPlanningActions();
        HideAllGhosts();
        PrepareCreepsForResolution();
        UpgradeState.OnResolutionStart();
        phase = Phase.Resolution;
    }

    // give every creep a fresh travel budget for the turn about to resolve; done here (not lazily in
    // creep movement) so ResolutionComplete never reads a stale parked flag when no new creeps spawned
    private void PrepareCreepsForResolution()
    {
        List<Unit> list = SpawnManager.GetActiveUnitList();
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null) continue;
            UnitCreep creep = list[i].GetCreep();
            if (creep == null) continue;
            creep.BeginTurnMovement();
        }
    }

    // ----- Economy -----

    private void GrantIncome()
    {
        int amount = GetIncomeForTurn(turnNumber) + UpgradeState.IncomeBonus();
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
            // a stunned creep can't move this turn, so it shouldn't block resolution
            if (!creep.IsParkedThisTurn() && !creep.IsStunned()) return false;
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
