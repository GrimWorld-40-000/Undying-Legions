using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// <see cref="LifeStageWorker_HumanlikeAdult.Notify_LifeStageStarted"/> reads <c>pawn.story.bodyType</c> without
/// null-checking <c>pawn.story</c>, which can be null during pawn generation when life stage is resolved early
/// for Necron pawns. Guard is intentionally limited to Necron races only — applying it universally breaks
/// non-Necron pawns (e.g. Odyssey DLC) that rely on this call to initialise headType.
/// </summary>
[HarmonyPatch(typeof(LifeStageWorker_HumanlikeAdult), nameof(LifeStageWorker_HumanlikeAdult.Notify_LifeStageStarted))]
public static class HarmonyPatch_LifeStageHumanlikeAdult
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn pawn, LifeStageDef previousLifeStage)
    {
        if (pawn?.story != null)
            return true;
        return !NechEnergyUtility.IsNecronPawn(pawn);
    }
}
