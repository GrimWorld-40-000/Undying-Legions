using System;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Player nechs with integrated projectile weapons (e.g. Spyder beamer): vanilla <see cref="MechConstant"/>
/// almost never queues ranged fire for drafted-adjacent idle fights.
/// </summary>
public class JobGiver_NechIntegratedRangedAutoAttack : ThinkNode_JobGiver
{
    /// <summary>Once per boot — avoids fill Player.log when a mod interaction throws every think tick.</summary>
    private const int ExceptionLogOnceKey = 0x554C4EC4;

    protected override Job TryGiveJob(Pawn pawn)
    {
        try
        {
            return TryGiveJobInternal(pawn);
        }
        catch (Exception ex)
        {
            Log.ErrorOnce(
                $"[Undying-Legions] JobGiver_NechIntegratedRangedAutoAttack (Spyder/nech ranged auto-job) failed for pawn {(pawn?.LabelShort ?? "?")}: {ex}",
                ExceptionLogOnceKey);
            return null;
        }
    }

    private static Job TryGiveJobInternal(Pawn pawn)
    {
        if (pawn == null || pawn.Downed || pawn.Dead || !pawn.Spawned)
            return null;
        if (pawn.Drafted)
            return null;
        if (pawn.Faction != Faction.OfPlayer)
            return null;

        // Humanlike NEC tree already uses JobGiver_NecronAutoAttack via HumanlikeConstant.
        if (pawn.RaceProps.Humanlike)
            return null;
        if (pawn.def?.GetModExtension<NecronMechExtension>() == null)
            return null;

        Verb ranged = NechIntegratedAttackUtility.TryGetPreferredRangedVerb(pawn, requireAvailable: true);
        if (ranged == null || ranged.verbProps == null)
            return null;

        if (pawn.jobs?.curJob?.playerForced == true)
            return null;

        JobQueue jq = pawn.jobs?.jobQueue;
        if (jq != null)
        {
            for (int i = 0; i < jq.Count; i++)
            {
                Job j = jq[i].job;
                if (j?.playerForced == true)
                    return null;
            }
        }

        float maxDist = ranged.verbProps.range;
        if (maxDist < 1f || float.IsNaN(maxDist) || float.IsInfinity(maxDist))
            return null;

        // BestAttackTarget signature: (searcher, flags, validator, minDist, maxDist, ...).
        // Pass 0f as minDist and maxDist as the upper bound — passing maxDist as the 4th arg
        // (without an explicit minDist) sets the MINIMUM distance to beamer range, which is
        // exactly backwards and causes the Spyder to target only enemies it cannot reach.
        IAttackTarget target = AttackTargetFinder.BestAttackTarget(
            pawn,
            TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
            x => (x is Pawn p && !p.Downed) || x is Building,
            0f,
            maxDist);

        if (target == null)
            return null;

        Job shootJob = JobMaker.MakeJob(JobDefOf.AttackStatic, (Thing)target);
        shootJob.verbToUse = ranged;
        shootJob.maxNumStaticAttacks = 1;
        shootJob.expiryInterval = 2000;
        shootJob.endIfCantShootInMelee = true;
        return shootJob;
    }
}
