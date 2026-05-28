using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Defensive guard for ideology conversion interaction on non-standard pawns.
/// Prevents vanilla/third-party interaction pipelines from throwing when either side
/// lacks ideology data (common with constructs/animals or modded pawn setups).
/// </summary>
[HarmonyPatch(typeof(InteractionWorker_ConvertIdeoAttempt), nameof(InteractionWorker_ConvertIdeoAttempt.Interacted))]
public static class HarmonyPatch_ConvertIdeoAttemptGuard
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn initiator, Pawn recipient)
    {
        if (initiator == null || recipient == null)
            return false;

        if (initiator.RaceProps?.Humanlike != true || recipient.RaceProps?.Humanlike != true)
            return false;

        if (initiator.ideo?.Ideo == null || recipient.ideo?.Ideo == null)
            return false;

        return true;
    }
}

/// <summary>
/// Lowborn Necrons are automations with no free will — they cannot initiate social interactions.
/// Blocks TryInteractWith at the source so they never start chitchat, social fights, etc.
/// </summary>
[HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractRandomly")]
public static class HarmonyPatch_LowBornNoSocial
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn ___pawn)
    {
        if (___pawn?.genes == null)
            return true;

        return ___pawn.genes.GenesListForReading?.Any(g =>
            g?.def?.defName == "GW_UD_LowBorn" ||
            g?.def?.defName == "GW_UD_LowBorn_FlayedOne") != true;
    }
}
