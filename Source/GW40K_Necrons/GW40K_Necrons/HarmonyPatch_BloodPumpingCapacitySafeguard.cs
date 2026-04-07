using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Same pattern as blood filtration / consciousness: during pawn generation
/// <see cref="PawnCapacityWorker_BloodPumping.CalculateCapacityLevel"/> can hit
/// <c>CalculateTagEfficiency</c> with an invalid <see cref="HediffSet"/> / body chain.
/// </summary>
[HarmonyPatch(typeof(PawnCapacityWorker_BloodPumping), nameof(PawnCapacityWorker_BloodPumping.CalculateCapacityLevel))]
public static class HarmonyPatch_BloodPumpingCapacitySafeguard
{
    [HarmonyPrefix]
    public static bool Prefix(HediffSet diffSet, List<PawnCapacityUtility.CapacityImpactor> impactors, ref float __result)
    {
        if (diffSet?.pawn != null && diffSet.pawn.RaceProps?.body != null)
            return true;

        __result = 1f;
        return false;
    }
}
