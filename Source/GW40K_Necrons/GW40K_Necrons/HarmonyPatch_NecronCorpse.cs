using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Necron corpses are inert chassis — not organic matter, not food, and not subject to decay.
/// </summary>
public static class NecronCorpseUtil
{
    public static bool IsNecronCorpse(Thing thing) =>
        thing is Corpse c && NechEnergyUtility.IsNecronPawn(c.InnerPawn);
}

// ── Not ingestible ────────────────────────────────────────────────────────────
// Gates both AI food-seeking (FoodUtility) and player-ordered ingestion.

[HarmonyPatch(typeof(Thing), "get_IngestibleNow")]
public static class HarmonyPatch_NecronCorpse_NotFood
{
    public static void Postfix(Thing __instance, ref bool __result)
    {
        if (__result && NecronCorpseUtil.IsNecronCorpse(__instance))
            __result = false;
    }
}

// ── No rot ────────────────────────────────────────────────────────────────────
// Skips CompRottable's tick entirely so RotProgress never advances.

[HarmonyPatch(typeof(CompRottable), nameof(CompRottable.CompTickRare))]
public static class HarmonyPatch_NecronCorpse_NoRot
{
    public static bool Prefix(CompRottable __instance)
    {
        return !NecronCorpseUtil.IsNecronCorpse(__instance.parent);
    }
}

// ── No "rotting" inspect label ────────────────────────────────────────────────

[HarmonyPatch(typeof(CompRottable), nameof(CompRottable.CompInspectStringExtra))]
public static class HarmonyPatch_NecronCorpse_NoRotLabel
{
    public static void Postfix(CompRottable __instance, ref string __result)
    {
        if (NecronCorpseUtil.IsNecronCorpse(__instance.parent))
            __result = null;
    }
}
