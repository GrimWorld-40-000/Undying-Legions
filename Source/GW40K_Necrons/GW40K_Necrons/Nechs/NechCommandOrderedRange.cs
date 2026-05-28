using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

public enum NechCommandRangeReject
{
    None,
    Leash,
    WeaponReach,
    TargetOutsideCommand
}

/// <summary>
/// Validates ordered jobs against the nechinator command radius.
/// Move / generic targets use overseer→target distance. Attack jobs enforce the commander leash
/// and require the target inside the command bubble; weapon reach is handled by the attack job
/// driver (pawn paths into melee or shooting range).
///
/// Attack orders are only denied when the target is BOTH outside the commander's control range
/// AND outside the Nech's own weapon range from its current position. If the Nech can reach
/// the target with its equipped weapon it may engage even past the leash boundary.
/// </summary>
public static class NechCommandOrderedRange
{
    public static bool AllTargetsWithinCommandRange(Pawn nech, Job job, HediffComp_NecronCommandTracker tracker) =>
        AllTargetsWithinCommandRange(nech, job, tracker, out _);

    public static bool AllTargetsWithinCommandRange(
        Pawn nech,
        Job job,
        HediffComp_NecronCommandTracker tracker,
        out NechCommandRangeReject reject)
    {
        reject = NechCommandRangeReject.None;

        if (job == null || tracker == null)
            return true;

        Pawn comm = HediffComp_NecronCommandTracker.GetCommanderOf(nech);
        if (comm == null || !comm.Spawned || nech.MapHeld == null || comm.MapHeld != nech.MapHeld)
            return true;

        float r = tracker.ControlRange;

        if (job.def == JobDefOf.AttackStatic || job.def == JobDefOf.AttackMelee)
            return AttackJobTargetsWithinCommand(nech, job, comm, r, out reject);

        if (!TargetOk(comm, r, job.targetA))
            return false;
        if (!TargetOk(comm, r, job.targetB))
            return false;
        if (!TargetOk(comm, r, job.targetC))
            return false;
        return true;
    }

    private static bool AttackJobTargetsWithinCommand(
        Pawn nech,
        Job job,
        Pawn comm,
        float controlRange,
        out NechCommandRangeReject reject)
    {
        reject = NechCommandRangeReject.None;

        // Only the TARGET's position is checked against the command radius —
        // the nech itself may be anywhere on the map. A drafted Nech that has
        // wandered or been pushed outside the leash must still be able to fire
        // at enemies inside the commander's bubble.

        if (!AttackJobTargetOk(nech, job, comm, controlRange, job.targetA))
        {
            reject = NechCommandRangeReject.TargetOutsideCommand;
            return false;
        }

        if (!AttackJobTargetOk(nech, job, comm, controlRange, job.targetB))
        {
            reject = NechCommandRangeReject.TargetOutsideCommand;
            return false;
        }

        if (!AttackJobTargetOk(nech, job, comm, controlRange, job.targetC))
        {
            reject = NechCommandRangeReject.TargetOutsideCommand;
            return false;
        }

        return true;
    }

    private static bool AttackJobTargetOk(Pawn nech, Job job, Pawn comm, float controlRange, LocalTargetInfo t)
    {
        if (!t.IsValid)
            return true;

        IntVec3 targetPos;
        if (t.HasThing)
        {
            Thing th = t.Thing;
            if (th == null || th.MapHeld != nech.MapHeld)
                return false;
            targetPos = th.Position;
        }
        else
        {
            targetPos = t.Cell;
        }

        // Allow if the target is inside the commander's control bubble.
        if (comm.Position.DistanceTo(targetPos) <= controlRange + 0.25f)
            return true;

        // Also allow if the target is within the Nech's own weapon range from its current
        // position — the Nech can physically engage without the commander closing in.
        float nechRange = NechWeaponRange(nech);
        return nech.Position.DistanceTo(targetPos) <= nechRange + 0.25f;
    }

    /// <summary>
    /// Returns the maximum attack range of the Nech's current effective verb (primary weapon
    /// for ranged Nechs, melee verb for unarmed / melee builds). Falls back to 1 tile.
    /// </summary>
    private static float NechWeaponRange(Pawn nech)
    {
        Verb v = nech.CurrentEffectiveVerb;
        if (v?.verbProps != null)
            return v.verbProps.range;

        // Secondary fallback: read range directly from the equipped weapon def.
        VerbProperties vp = nech.equipment?.Primary?.def?.Verbs?[0];
        if (vp != null)
            return vp.range;

        return 1f; // unarmed melee fallback
    }

    private static bool TargetOk(Pawn comm, float range, LocalTargetInfo t)
    {
        if (!t.IsValid)
            return true;
        if (t.HasThing)
        {
            Thing th = t.Thing;
            if (th == null || th.MapHeld != comm.MapHeld)
                return false;
            return comm.Position.DistanceTo(th.Position) <= range;
        }

        return comm.Position.DistanceTo(t.Cell) <= range;
    }
}
