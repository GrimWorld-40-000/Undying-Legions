using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Validates that ordered job targets lie within the nechinator command radius (from overseer position).
/// </summary>
public static class NechCommandOrderedRange
{
    public static bool AllTargetsWithinCommandRange(Pawn nech, Job job, HediffComp_NecronCommandTracker tracker)
    {
        if (job == null || tracker == null)
            return true;

        Pawn comm = HediffComp_NecronCommandTracker.GetCommanderOf(nech);
        if (comm == null || !comm.Spawned || nech.MapHeld == null || comm.MapHeld != nech.MapHeld)
            return true;

        float r = tracker.ControlRange;

        if (!TargetOk(comm, r, job.targetA))
            return false;
        if (!TargetOk(comm, r, job.targetB))
            return false;
        if (!TargetOk(comm, r, job.targetC))
            return false;
        return true;
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
