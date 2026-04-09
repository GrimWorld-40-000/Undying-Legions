using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Restricts drafted, player-issued Nech orders so every job target lies within command range of the overseer.
/// Autonomous behavior (work/wander/auto-attack) is unaffected.
/// </summary>
[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob), typeof(Job), typeof(JobTag), typeof(bool))]
public static class HarmonyPatch_NechOrderedJobRange
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn ___pawn, Job job, bool requestQueueing)
    {
        if (___pawn == null || job == null)
            return true;
        if (___pawn.def.GetModExtension<NecronMechExtension>() == null)
            return true;
        if (!___pawn.Drafted)
            return true;

        // Draft-in / standby without a meaningful world target.
        if (job.def == JobDefOf.Wait_Combat)
            return true;

        Pawn commander = ___pawn.GetOverseer();
        HediffComp_NecronCommandTracker tracker = HediffComp_NecronCommandTracker.GetTracker(commander);
        if (tracker == null || !tracker.controlledMechs.Contains(___pawn))
            return true;

        if (NechCommandOrderedRange.AllTargetsWithinCommandRange(___pawn, job, tracker))
            return true;

        Messages.Message(
            "GW40K_CommandOutOfRangeDraftedOnlyMove".Translate(tracker.ControlRange.ToString("0.#")),
            MessageTypeDefOf.RejectInput,
            false);
        return false;
    }
}
