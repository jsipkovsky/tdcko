using System.Collections.Generic;
using TDTK;
using UnityEngine;

// Central registry for the real tower upgrades the player picks from the modifier popup.
// ModifierManager records a pick by calling Activate(id); the rest of the game (Unit stat getters,
// the attack pipeline, TurnManager economy) queries the static helpers here. Everything is keyed by
// tower prefabID so a picked upgrade retroactively affects every tower of that type.
public static class UpgradeState
{
    // combat tower prefabIDs (see Assets/TDTK/Prefabs/Towers/*.prefab)
    private const int SPEAR = 0;
    private const int BLADE = 1;
    private const int BALISTA = 2;
    private const int SNIPER = 3;
    private const int SST = 8;

    private static readonly HashSet<string> active = new HashSet<string>();

    // turn a given time-limited upgrade was picked (for this-turn / next-turn windows)
    private static int hastyTurn = -100;
    private static int noRiskTurn = -100;

    // seconds elapsed in the current monster (resolution) turn; driven by TurnManager
    public static float ResolutionTime = 0f;

    public static void Reset()
    {
        active.Clear();
        hastyTurn = -100;
        noRiskTurn = -100;
        ResolutionTime = 0f;
    }

    public static bool IsActive(string id) { return active.Contains(id); }

    public static void Activate(string id)
    {
        active.Add(id);
        if (id == "hasty_investment") hastyTurn = TurnManager.turnNumber;
        if (id == "no_risk") noRiskTurn = TurnManager.turnNumber;
    }

    // ---------- stat modifiers (read live by Unit getters) ----------

    // multiplicative damage bonus (sharp as a pencil, big guns)
    public static float DmgMul(int prefabID)
    {
        float m = 1f;
        if (prefabID == SPEAR)
        {
            if (active.Contains("sharp_pencil")) m *= 1.3f;
            if (active.Contains("big_guns")) m *= 2f;
        }
        return m;
    }

    // flat damage bonus added to both min and max (once is enough)
    public static float DmgMod(int prefabID)
    {
        float v = 0f;
        if (prefabID == SST && active.Contains("once_is_enough")) v += 4f;
        return v;
    }

    public static float RangeMul(int prefabID)
    {
        if (prefabID == SPEAR && active.Contains("big_guns")) return 0.5f;
        return 1f;
    }

    // splash radius added on hit (big guns)
    public static float AoeMod(int prefabID)
    {
        if (prefabID == SPEAR && active.Contains("big_guns")) return 1.5f;
        return 0f;
    }

    // flat cooldown change in seconds (cheap gears makes the ballista slower)
    public static float CooldownMod(int prefabID)
    {
        if (prefabID == BALISTA && active.Contains("cheap_gears")) return 1f;
        return 0f;
    }

    // multiplicative cooldown change (shot in the dark, tough shift base speed-up)
    public static float CooldownMul(int prefabID)
    {
        float m = 1f;
        if (prefabID == SNIPER && active.Contains("shot_in_dark")) m *= 0.5f;
        if (prefabID == BLADE && active.Contains("tough_shift")) m *= 0.5f;
        return m;
    }

    // extra simultaneous targets per attack (spare material)
    public static int TargetCountMod(int prefabID)
    {
        if (prefabID == BALISTA && active.Contains("spare_material")) return 1;
        return 0;
    }

    // ---------- economy ----------

    // flat gold change to a tower's build cost (cheap gears; negative = cheaper)
    public static float CostMod(int prefabID)
    {
        if (prefabID == BALISTA && active.Contains("cheap_gears")) return -10f;
        return 0f;
    }

    // global cost multiplier for the current turn (hasty investment)
    public static float GlobalCostMul()
    {
        if (active.Contains("hasty_investment"))
        {
            if (TurnManager.turnNumber == hastyTurn) return 0.8f;
            if (TurnManager.turnNumber == hastyTurn + 1) return 1.25f;
        }
        return 1f;
    }

    public static int IncomeBonus() { return active.Contains("sandwich_investor") ? 10 : 0; }

    // called at the end of a player turn; applies the one-shot "no risk no fun" payout
    public static void OnPlayerTurnEnd()
    {
        if (active.Contains("no_risk") && TurnManager.turnNumber == noRiskTurn + 1)
        {
            RscManager.MultiplyResources(1.6f);
            noRiskTurn = -100; // one-shot
        }
    }

    // ---------- targeting / combat behaviour ----------

    public static Unit._TargetMode OverrideTargetMode(Unit tower, Unit._TargetMode baseMode)
    {
        if (tower.prefabID == SNIPER && active.Contains("shot_in_dark"))
        {
            if (Random.value < 0.7f) return Unit._TargetMode.Random;
        }
        return baseMode;
    }

    public const float ChainRange = 2f;
    public const int ChainCount = 2;
    public static bool SpearChains(Unit src) { return src != null && src.prefabID == SPEAR && active.Contains("sharpest_tool"); }

    public static bool BladeImmobilizes(Unit t) { return t.prefabID == BLADE && active.Contains("wait_and_see"); }
    public static bool BladeToughShift(Unit t) { return t.prefabID == BLADE && active.Contains("tough_shift"); }
    public static bool BalistaFlaming(Unit src) { return src != null && src.prefabID == BALISTA && active.Contains("flaming_shots"); }
    public static bool SSTKillingSpree(Unit t) { return t.prefabID == SST && active.Contains("killing_spree"); }
    public static bool SniperAllOrNothing(Unit t) { return t.prefabID == SNIPER && active.Contains("all_or_nothing"); }

    // ---------- build limits ----------

    public static int EffectiveBuildLimit(int typeID, int limit)
    {
        if (typeID == SST && active.Contains("once_is_enough")) return 1;
        return limit;
    }

    // ---------- turn lifecycle ----------

    // called when a monster (resolution) turn starts; resets timers and per-tower turn state
    public static void OnResolutionStart()
    {
        ResolutionTime = 0f;
        List<UnitTower> towers = TowerManager.GetActiveTowerList();
        if (towers == null) return;
        for (int i = 0; i < towers.Count; i++)
            if (towers[i] != null) towers[i].UpgradeOnResolutionStart();
    }

    // ---------- runtime effect factories ----------

    public static Effect MakeStunEffect(Unit src, float duration)
    {
        Effect e = new Effect();
        e.SetAsModifier();
        e.stun = true;
        e.duration = duration;
        e.prefabID = 90001;
        e.ID = 90001;
        e.srcType = Effect._SrcType.Tower;
        e.srcPrefabID = src.prefabID;
        e.stackable = false;
        e.hitVisualEffect = new VisualObject();
        return e;
    }

    public static Effect MakeBurnEffect(Unit src, float dps, float duration)
    {
        Effect e = new Effect();
        e.SetAsModifier();
        e.stats.hpRate = -dps;
        e.duration = duration;
        e.prefabID = 90002;
        e.ID = 90002;
        e.srcType = Effect._SrcType.Tower;
        e.srcPrefabID = src.prefabID;
        e.stackable = false;
        e.hitVisualEffect = new VisualObject();
        return e;
    }
}
