using System.Collections.Generic;
using UnityEngine;
using TDTK;

// Every couple of turns, assigns a random special effect to a few build platforms.
// A tower built on a special platform bakes in that effect for its lifetime (see UnitTower).
// Old assignments are cleared on each reroll; towers already built keep the effect they were built with.
public static class PlatformEffectManager
{
    // how often (in turns) the specials are rerolled; starts at turn 2, then 4, 6, ...
    public const int cadence = 2;
    // how many platforms get a special each reroll
    public const int platformsPerReroll = 3;

    // effect id -> hover description (ids match the handling in UnitTower/BuildPlatform/TowerManager)
    private static readonly string[] descriptions = new string[]
    {
        "",
        "Special: towers built here cost 15% less but deal 15% less damage.",
        "Special: towers built here skip their first attack turn, then attack 20% faster.",
        "Special: towers built here cost 30% more but generate 50% more gold on kill.",
        "Special: towers built here can't attack; they add their damage to the nearest same-type tower.",
    };

    public static void RerollIfDue(int turnNumber)
    {
        if (turnNumber < cadence) return;
        if (turnNumber % cadence != 0) return;
        Reroll();
    }

    private static void Reroll()
    {
        BuildPlatform[] all = Object.FindObjectsOfType<BuildPlatform>();

        // clear previous specials so freshly built towers only inherit the current assignment
        List<BuildPlatform> eligible = new List<BuildPlatform>();
        for (int i = 0; i < all.Length; i++)
        {
            BuildPlatform p = all[i];
            if (p == null) continue;
            p.ClearSpecial();
            if (p.enabled && p.gameObject.activeInHierarchy) eligible.Add(p);
        }

        int count = Mathf.Min(platformsPerReroll, eligible.Count);
        int effect = Random.Range(1, descriptions.Length); // 1..4, same effect for all platforms this reroll
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, eligible.Count);
            BuildPlatform p = eligible[idx];
            eligible.RemoveAt(idx);

            p.SetSpecial(effect, descriptions[effect]);
        }
    }
}
