using NecronGeneUtil;
using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Defend mode for Canoptek scarabs:
///   • 4-second grace period after mode is set — returns null so the pawn finishes
///     whatever it was doing before committing to defend behaviour.
///   • Attack the nearest hostile within <see cref="CommandRange"/> of the controller.
///   • When no threats exist, follow / patrol around the controller.
/// Only fires when the scarab's ControlNodeMode is Defend.
/// </summary>
public class JobGiver_ScarabDefendController : ThinkNode_JobGiver
{
    private const int   GraceTicks    = 240;  // 4 game-seconds before defending kicks in
    private const float SearchRadius  = 30f;  // how far to search for the controller
    private const float CommandRange  = 18f;  // radius around controller to engage threats
    private const float FollowRadius  = 8f;   // follow job leash radius
    private const float CloseEnoughDist = 5f; // don't issue a follow job if already this close

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Map == null) return null;
        if (!NecrodermisIngestionUtility.IsCanoptek(pawn)) return null;

        var store = GameComponent_CanoptekConstructModes.Current;
        if (store == null || store.GetMode(pawn) != ControlNodeMode.Defend) return null;

        // Grace period: wait 4 game-seconds after defend mode is set before acting.
        int setTick = store.GetDefendSetTick(pawn);
        if (Find.TickManager.TicksGame - setTick < GraceTicks) return null;

        // Find the nearest allied humanlike Necron to defend (controller).
        Pawn controller = FindController(pawn);
        if (controller == null) return null;

        // Attack the nearest hostile within command range of the controller.
        Pawn threat = FindThreatNearController(pawn, controller);
        if (threat != null)
            return JobMaker.MakeJob(JobDefOf.AttackMelee, threat);

        // No threat — patrol by following the controller.
        float distSq = (controller.Position - pawn.Position).LengthHorizontalSquared;
        if (distSq <= CloseEnoughDist * CloseEnoughDist)
            return null; // already close; fall through to animal wander near controller

        Job follow = JobMaker.MakeJob(JobDefOf.Follow, controller);
        follow.followRadius = FollowRadius;
        return follow;
    }

    private static Pawn FindController(Pawn pawn)
    {
        Pawn best = null;
        float bestDist = SearchRadius * SearchRadius;
        foreach (Pawn candidate in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))
        {
            if (candidate == pawn || !candidate.RaceProps.Humanlike || candidate.Dead) continue;
            float d = (candidate.Position - pawn.Position).LengthHorizontalSquared;
            if (d < bestDist) { bestDist = d; best = candidate; }
        }
        return best;
    }

    private static Pawn FindThreatNearController(Pawn pawn, Pawn controller)
    {
        Pawn best = null;
        float bestDist = CommandRange * CommandRange;
        foreach (Pawn other in pawn.Map.mapPawns.AllPawnsSpawned)
        {
            if (other.Dead || other.Downed) continue;
            if (!GenHostility.HostileTo(pawn, other)) continue;
            float d = (other.Position - controller.Position).LengthHorizontalSquared;
            if (d >= bestDist) continue;
            if (!pawn.CanReach(other, PathEndMode.Touch, Danger.Deadly)) continue;
            bestDist = d;
            best = other;
        }
        return best;
    }
}
