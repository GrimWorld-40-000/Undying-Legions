using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Same failure mode as consciousness: <see cref="PawnCapacityWorker_BloodFiltration.CalculateCapacityLevel"/>
/// starts with <c>diffSet.pawn.RaceProps.body</c>. During early pawn generation that chain can be null and NRE.
/// </summary>
[HarmonyPatch(typeof(PawnCapacityWorker_BloodFiltration), nameof(PawnCapacityWorker_BloodFiltration.CalculateCapacityLevel))]
public static class HarmonyPatch_BloodFiltrationCapacitySafeguard
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
