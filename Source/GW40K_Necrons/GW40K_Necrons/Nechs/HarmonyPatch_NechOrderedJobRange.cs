using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
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
        if (!NechUtility.IsNechControlled(___pawn))
            return true;

        // Enemy Necrons (raids, sieges) have no player-side Nechinator and never need
        // commander checks. Without this guard the "uncontrolled" message fires and the
        // job is blocked whenever an enemy Spyder tries to cast the particle cannon.
        if (___pawn.Faction != Faction.OfPlayer)
            return true;

        // Humanlike Necrons outside the Nech command system (e.g. Crypteks, Overlords) behave as normal colonists.
        if (___pawn.RaceProps.Humanlike && !NechUtility.IsHumanlikeNechControlled(___pawn))
            return true;

        // Draft-in / standby without a meaningful world target — always allow.
        if (job.def == JobDefOf.Wait_Combat)
            return true;

        Pawn commander = HediffComp_NecronCommandTracker.GetCommanderOf(___pawn);
        bool controlNodeLinked = HediffComp_ControlNodeTracker.GetControllerOfConstruct(___pawn) != null;

        // Particle beamer: bypass ALL range/control checks before the uncontrolled gate.
        // CompTickRare auto-attack issues AttackStatic jobs for unlinked or undrafted Spyders;
        // blocking them at the uncontrolled check prevents auto-attack from ever working.
        if (IsParticleBeamerJob(___pawn, job))
            return true;

        // Uncontrolled Nechs cannot receive any ordered job. Control-node scarabs (Spyder/Cryptek link)
        // have no Command Protocol commander — only GetControllerOfConstruct — so they must not fail this gate.
        if (commander == null && !controlNodeLinked)
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

        // Drafted + Command Protocol: enforce nechinator command range. No protocol commander — nothing to clamp.
        if (commander == null)
            return true;

        // Drafted and controlled — enforce command range.
        HediffComp_NecronCommandTracker tracker = HediffComp_NecronCommandTracker.GetTracker(commander);
        if (tracker == null || !tracker.controlledMechs.Contains(___pawn))
            return true;

        // Ability casts (JobDriver_CastAbility*) carry world/cell targets that are not "commander to nech"
        // distance; blocking them breaks self-buffs (e.g. Flayed ghostwind) and shows range errors.
        if (IsAbilityCastJob(job))
        {
            requestQueueing = false;
            return true;
        }

        // Particle cannon: its attack range (17) is the governing limit, not the commander's
        // command radius. Blocking it would prevent the player from targeting anything outside
        // the commander's leash even though the weapon itself can reach further.
        if (IsParticleBeamerJob(___pawn, job))
            return true;

        if (NechCommandOrderedRange.AllTargetsWithinCommandRange(___pawn, job, tracker, out NechCommandRangeReject deny))
            return true;

        // For move orders, reroute to the nearest valid cell inside command range rather than rejecting.
        if (TryRedirectMoveToRangeEdge(___pawn, job, tracker))
            return true;

        TaggedString msg = deny switch
        {
            NechCommandRangeReject.WeaponReach => "GW40K_CommandAttackOutOfWeaponRange".Translate(),
            _ => "GW40K_CommandOutOfRangeDraftedOnlyMove".Translate(tracker.ControlRange.ToString("0.#")),
        };
        Messages.Message(msg, MessageTypeDefOf.RejectInput, false);
        return false;
    }

    /// <summary>
    /// For Goto jobs targeting a cell beyond command range, redirects targetA to the nearest
    /// standable cell inside the range circle along the direction to the original target.
    /// Returns true if a valid redirect was found and applied.
    /// </summary>
    private static bool TryRedirectMoveToRangeEdge(Pawn nech, Job job, HediffComp_NecronCommandTracker tracker)
    {
        if (job.def != JobDefOf.Goto) return false;
        if (!job.targetA.IsValid || job.targetA.HasThing) return false;

        Pawn comm = HediffComp_NecronCommandTracker.GetCommanderOf(nech);
        if (comm == null || !comm.Spawned || nech.MapHeld == null) return false;

        IntVec3 commPos = comm.Position;
        float range = tracker.ControlRange;
        Map map = nech.MapHeld;

        // Project a point just inside the range circle edge toward the requested target.
        Vector3 dir = job.targetA.Cell.ToVector3Shifted() - commPos.ToVector3Shifted();
        if (dir.sqrMagnitude < 0.01f) return false;
        dir = dir.normalized;
        IntVec3 edgeCell = (commPos.ToVector3Shifted() + dir * (range - 1f)).ToIntVec3();

        // Spiral outward from that edge point to find the nearest standable cell within range.
        foreach (IntVec3 c in GenRadial.RadialCellsAround(edgeCell, 5, true))
        {
            if (!c.InBounds(map) || !c.Standable(map)) continue;
            if (commPos.DistanceTo(c) > range) continue;
            if (c == nech.Position) continue;
            job.targetA = c;
            return true;
        }

        return false;
    }

    private static bool IsAbilityCastJob(Job job)
    {
        if (job?.def?.driverClass == null)
            return false;
        string name = job.def.driverClass.Name ?? string.Empty;
        return name.IndexOf("CastAbility", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// True when the pawn is a Spyder and the job is an AttackStatic using the integrated
    /// particle beamer verb. The cannon's own range (17) governs targeting; enforcing the
    /// commander's leash radius would prevent firing beyond that radius even with a valid target.
    /// </summary>
    private static bool IsParticleBeamerJob(Pawn pawn, Job job)
    {
        if (job?.def != JobDefOf.AttackStatic) return false;
        return pawn.verbTracker?.AllVerbs?.Exists(v => v is Verb_SpyderParticleBeamer) == true;
    }
}
