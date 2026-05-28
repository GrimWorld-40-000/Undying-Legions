using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Escort mode idle — wanders near the commander's position.
/// Mirrors <c>JobGiver_WanderOverseer</c>; uses our Necron command tracker.
/// </summary>
public class JobGiver_NechWanderCommander : JobGiver_Wander
{
    public JobGiver_NechWanderCommander()
    {
        wanderRadius = 7f;
        ticksBetweenWandersRange = new IntRange(125, 200);
    }

    protected override IntVec3 GetWanderRoot(Pawn pawn)
    {
        Pawn commander = HediffComp_NecronCommandTracker.GetCommanderOf(pawn);
        return commander?.Position ?? pawn.Position;
    }

    protected override void DecorateGotoJob(Job job)
    {
        job.expiryInterval = 120;
        job.expireRequiresEnemiesNearby = true;
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
        Pawn commander = HediffComp_NecronCommandTracker.GetCommanderOf(pawn);
        if (commander == null || !commander.Spawned || commander.Map != pawn.Map) return null;

        Job job = base.TryGiveJob(pawn);
        if (job != null)
            job.reportStringOverride = "Escorting".Translate(commander.Named("TARGET"));
        return job;
    }
}
