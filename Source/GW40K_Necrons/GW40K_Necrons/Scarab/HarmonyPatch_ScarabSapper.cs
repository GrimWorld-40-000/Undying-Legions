using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>Scarabs are non-humanlike and fail vanilla mining checks; treat them as sappers when flagged on kind.</summary>
[HarmonyPatch(typeof(SappersUtility), nameof(SappersUtility.IsGoodSapper))]
internal static class HarmonyPatch_ScarabSapper_IsGoodSapper
{
    private static void Postfix(Pawn p, ref bool __result)
    {
        if (__result || p == null)
            return;
        if (!ScarabRaidDutyUtility.IsScarab(p) || !p.kindDef.canBeSapper)
            return;

        __result = DefDatabase<AbilityDef>.GetNamedSilentFail("GW40K_ScarabSelfDestruct") != null;
    }
}
