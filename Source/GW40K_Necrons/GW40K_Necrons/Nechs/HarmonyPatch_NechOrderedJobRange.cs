using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Blocks all ordered jobs on uncontrolled Nechs; for controlled drafted Nechs, enforces command range.
/// Autonomous behavior (work/wander/auto-attack) is unaffected.
/// </summary>
[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob), typeof(Job), typeof(JobTag), typeof(bool))]
public static class HarmonyPatch_NechOrderedJobRange
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn ___pawn, Job job, ref bool requestQueueing)
    {
        if (___pawn == null || job == null)
            return true;
        if (___pawn.def.GetModExtension<NecronMechExtension>() == null)
            return true;

        // Humanlike Necrons other than Flayed colonist race behave like normal colonists for orders.
        // Humanlike Flayed (race GW40K_NecronFlayedOne) uses the same uncontrolled / range rules as Nechs.
        if (___pawn.RaceProps.Humanlike && !FlayedHumanlikeNechUtility.IsHumanlikeFlayedNechRace(___pawn))
            return true;

        // Draft-in / standby without a meaningful world target — always allow.
        if (job.def == JobDefOf.Wait_Combat)
            return true;

        Pawn commander = HediffComp_NecronCommandTracker.GetCommanderOf(___pawn);

        // Uncontrolled Nechs cannot receive any ordered job.
        if (commander == null)
        {
            Messages.Message(
                "GW40K_NechCannotOrderUncontrolled".Translate(___pawn.LabelShortCap),
                MessageTypeDefOf.RejectInput,
                false);
            return false;
        }

        // Controlled but not drafted — allow ordered jobs without range restriction.
        if (!___pawn.Drafted)
            return true;

        // Drafted and controlled — enforce command range.
        HediffComp_NecronCommandTracker tracker = HediffComp_NecronCommandTracker.GetTracker(commander);
        if (tracker == null || !tracker.controlledMechs.Contains(___pawn))
            return true;

        // Ability casts (JobDriver_CastAbility*) carry world/cell targets that are not "commander to nech"
        // distance; blocking them breaks self-buffs (e.g. Flayed ghostwalk) and shows range errors.
        if (IsAbilityCastJob(job))
        {
            // Self-buffs and other ability casts should fire immediately on click for Nechs.
            // Queued cast jobs can remain stuck behind drafted wait and show "ability queued".
            requestQueueing = false;
            return true;
        }

        if (NechCommandOrderedRange.AllTargetsWithinCommandRange(___pawn, job, tracker))
            return true;

        Messages.Message(
            "GW40K_CommandOutOfRangeDraftedOnlyMove".Translate(tracker.ControlRange.ToString("0.#")),
            MessageTypeDefOf.RejectInput,
            false);
        return false;
    }

    private static bool IsAbilityCastJob(Job job)
    {
        if (job?.def?.driverClass == null)
            return false;
        string name = job.def.driverClass.Name ?? string.Empty;
        return name.IndexOf("CastAbility", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
