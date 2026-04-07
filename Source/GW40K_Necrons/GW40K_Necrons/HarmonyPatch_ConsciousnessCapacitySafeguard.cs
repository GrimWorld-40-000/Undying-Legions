using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Vanilla <see cref="PawnCapacityUtility.CalculateTagEfficiency"/> assumes <c>diffSet.pawn</c> and <c>RaceProps.body</c> are non-null.
/// During pawn generation (e.g. <see cref="PawnGenerator.GenerateInitialHediffs"/>), those can be unset briefly and cause an NRE
/// inside <see cref="Pawn_HealthTracker.ShouldBeDead"/> → consciousness checks. This prefix skips that path safely.
/// </summary>
[HarmonyPatch(typeof(PawnCapacityWorker_Consciousness), nameof(PawnCapacityWorker_Consciousness.CalculateCapacityLevel))]
public static class HarmonyPatch_ConsciousnessCapacitySafeguard
{
    [HarmonyPrefix]
    public static bool Prefix(HediffSet diffSet, List<PawnCapacityUtility.CapacityImpactor> impactors, ref float __result)
    {
        if (diffSet?.pawn != null && diffSet.pawn.RaceProps?.body != null)
        {
            return true;
        }

        // Matches vanilla CalculateTagEfficiency when no tagged parts exist (returns 1f before pain/blood lerps).
        __result = 1f;
        return false;
    }
}
