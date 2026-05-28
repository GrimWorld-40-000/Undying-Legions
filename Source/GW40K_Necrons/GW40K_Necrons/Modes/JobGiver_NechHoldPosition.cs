using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Hold mode — wanders within a very tight radius of the position stored in
/// <see cref="ThingComp_NechWorkMode.HeldPosition"/>, then waits in combat-ready stance.
/// Combat is handled at higher priority by the think tree's existing responders.
/// </summary>
public class JobGiver_NechHoldPosition : JobGiver_Wander
{
    public JobGiver_NechHoldPosition()
    {
        wanderRadius = 2f;
        ticksBetweenWandersRange = new IntRange(60, 120);
    }

    protected override IntVec3 GetWanderRoot(Pawn pawn)
    {
        ThingComp_NechWorkMode comp = pawn.TryGetComp<ThingComp_NechWorkMode>();
        IntVec3 held = comp?.HeldPosition ?? IntVec3.Invalid;
        return held.IsValid ? held : pawn.Position;
    }

    protected override void DecorateGotoJob(Job job)
    {
        job.expiryInterval = 200;
        job.expireRequiresEnemiesNearby = true;
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
        Job wanderJob = base.TryGiveJob(pawn);
        if (wanderJob != null) return wanderJob;

        // Nothing to wander to — stand at current position in combat-ready stance.
        Job waitJob = JobMaker.MakeJob(JobDefOf.Wait_Combat);
        waitJob.expiryInterval = 300;
        return waitJob;
    }
}
