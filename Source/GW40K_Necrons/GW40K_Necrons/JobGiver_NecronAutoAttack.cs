using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Replaces JobGiver_ConfigurableHostilityResponse for Necrons.
/// Always issues an attack job against the nearest valid hostile — no flee or ignore modes.
/// </summary>
public class JobGiver_NecronAutoAttack : ThinkNode_JobGiver
{
    protected override Job TryGiveJob(Pawn pawn)
    {
        if (pawn.Downed || pawn.Faction == null || !pawn.IsColonistPlayerControlled)
            return null;

        // Don't override direct player orders
        if (pawn.jobs?.curJob?.playerForced == true)
            return null;
        if (pawn.jobs?.jobQueue != null)
        {
            foreach (QueuedJob qj in pawn.jobs.jobQueue)
                if (qj.job.playerForced) return null;
        }

        IAttackTarget target = AttackTargetFinder.BestAttackTarget(
            pawn,
            TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
            x => x is Pawn { Downed: false } || x is Building,
            maxDist: 6f);

        if (target == null)
            return null;

        if (pawn.equipment?.Primary != null && !pawn.equipment.Primary.def.IsMeleeWeapon)
        {
            Job shootJob = JobMaker.MakeJob(JobDefOf.AttackStatic, (Thing)target);
            shootJob.maxNumStaticAttacks = 1;
            shootJob.expiryInterval = 2000;
            shootJob.endIfCantShootInMelee = true;
            return shootJob;
        }

        return MeleeAttackJobFor(pawn, target);
    }

    private static Job MeleeAttackJobFor(Pawn pawn, IAttackTarget target)
    {
        Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, (Thing)target);
        job.maxNumMeleeAttacks = 1;
        job.expiryInterval = 2000;
        return job;
    }
}
