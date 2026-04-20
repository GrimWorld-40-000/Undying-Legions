using System.Collections.Generic;
using HarmonyLib;
using NecronGeneUtil;
using RimWorld;
using UnityEngine;
using Verse;

namespace GW40K_Necrons;

// While a necrodermis-bearing Necron wears a damaged dispersion shield the living
// metal slowly self-repairs. The regeneration consumes 20 % extra necrodermis per
// day for as long as the shield HP is below maximum.

[HarmonyPatch(typeof(Need_Necrodermis), nameof(Need_Necrodermis.NeedInterval))]
public static class HarmonyPatch_NecrodermisShieldRegen
{
    internal const string ShieldDefName    = "GM40k_Necron_Shield";
    internal const float  HpPerDay         = 120f;   // HP restored per in-game day (full repair from 0 ≈ 10 days)
    internal const float  ExtraDrainFactor = 0.20f;  // fraction of base fallPerDay added as extra drain
    private  const int    IntervalTicks    = 150;     // matches Need.NeedInterval cadence

    // Fractional HP accumulation keyed by shield thingIDNumber.
    // Not persisted — losing < 1 HP progress on save/load is negligible.
    private static readonly Dictionary<int, float> s_regenAccumulators = new();

    public static void Postfix(Need __instance)
    {
        Pawn pawn = Traverse.Create(__instance).Field<Pawn>("pawn").Value;
        if (pawn?.apparel == null) return;

        Apparel shield = FindShield(pawn);
        if (shield == null || shield.HitPoints >= shield.MaxHitPoints) return;

        // Extra necrodermis drain while repair is active
        float extraDrain = __instance.def.fallPerDay * ExtraDrainFactor / 60000f * IntervalTicks;
        __instance.CurLevel = Mathf.Max(0f, __instance.CurLevel - extraDrain);

        // Fractional HP regen — accumulate sub-1-HP gains until a whole point is ready
        int id = shield.thingIDNumber;
        if (!s_regenAccumulators.TryGetValue(id, out float acc))
            acc = 0f;

        acc += HpPerDay / 60000f * IntervalTicks;
        int heal = Mathf.FloorToInt(acc);
        if (heal > 0)
        {
            shield.HitPoints = Mathf.Min(shield.HitPoints + heal, shield.MaxHitPoints);
            acc -= heal;
        }
        s_regenAccumulators[id] = acc;
    }

    private static Apparel FindShield(Pawn pawn)
    {
        foreach (Apparel a in pawn.apparel.WornApparel)
            if (a.def.defName == ShieldDefName)
                return a;
        return null;
    }
}
