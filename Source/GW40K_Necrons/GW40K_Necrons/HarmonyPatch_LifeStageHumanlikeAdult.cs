using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// <see cref="LifeStageWorker_HumanlikeAdult.Notify_LifeStageStarted"/> reads <c>pawn.story.bodyType</c> without
/// null-checking <c>pawn.story</c>, which can still be null during pawn generation when life stage is resolved early.
/// </summary>
[HarmonyPatch(typeof(LifeStageWorker_HumanlikeAdult), nameof(LifeStageWorker_HumanlikeAdult.Notify_LifeStageStarted))]
public static class HarmonyPatch_LifeStageHumanlikeAdult
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn pawn, LifeStageDef previousLifeStage)
    {
        return pawn?.story != null;
    }
}
