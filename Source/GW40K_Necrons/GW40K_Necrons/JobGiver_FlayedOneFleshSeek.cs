using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Drives a Flayed One to seek flesh autonomously before the starvation threshold.
///
/// Phase 1 (food below <see cref="SeekThreshold"/>): finds and eats the nearest
/// accessible corpse or raw meat chunk within <see cref="SearchRadius"/> tiles.
///
/// Phase 2 (food below <see cref="HuntThreshold"/>): if no flesh is available,
/// hunts the nearest valid living target using the vanilla PredatorHunt job.
/// Never targets player colonists, colony prisoners, or pawns sharing the Flayed One's faction.
/// Never targets other Necron constructs (NecronMechExtension).
/// </summary>
public class JobGiver_FlayedOneFleshSeek : ThinkNode_JobGiver
{
    private const float SeekThreshold = 0.60f;
    private const float HuntThreshold = 0.40f;
    private const float SearchRadius   = 30f;

    // Tracks pawns that have already received a seeking notification this episode.
    private static readonly System.Collections.Generic.HashSet<int> _notifiedPawns = new();

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (pawn.Downed) return null;

        Need_Food food = pawn.needs?.food;
        if (food == null || food.CurLevelPercentage >= SeekThreshold)
        {
            _notifiedPawns.Remove(pawn.thingIDNumber);
            return null;
        }

        if (_notifiedPawns.Add(pawn.thingIDNumber) && PawnUtility.ShouldSendNotificationAbout(pawn))
        {
            Messages.Message(
                "GW40K_FlayedOneSeekingFlesh".Translate(pawn.Named("PAWN")),
                pawn,
                MessageTypeDefOf.NegativeEvent);
        }

        // Phase 1: eat nearby flesh (corpse / raw meat chunk)
        Thing flesh = FindNearbyFlesh(pawn);
        if (flesh != null)
            return JobMaker.MakeJob(JobDefOf.Ingest, flesh);

        // Phase 2: hunt a living creature only if hungry enough
        if (food.CurLevelPercentage >= HuntThreshold)
            return null;

        Pawn prey = FindPrey(pawn);
        if (prey != null)
            return JobMaker.MakeJob(JobDefOf.PredatorHunt, prey);

        return null;
    }

    private static Thing FindNearbyFlesh(Pawn pawn)
    {
        return GenClosest.ClosestThingReachable(
            pawn.Position,
            pawn.Map,
            ThingRequest.ForGroup(ThingRequestGroup.FoodSourceNotPlantOrTree),
            PathEndMode.ClosestTouch,
            TraverseParms.For(pawn),
            SearchRadius,
            t => t.IngestibleNow
              && !t.IsForbidden(pawn)
              && pawn.CanReserve(t)
              && pawn.WillEat(t)
              && !(t is Pawn { Dead: false })); // living pawns go through hunting, not direct ingestion
    }

    private static Pawn FindPrey(Pawn pawn)
    {
        Pawn best    = null;
        float bestDist = float.MaxValue;
        float radiusSq = SearchRadius * SearchRadius;

        foreach (Pawn candidate in pawn.Map.mapPawns.AllPawnsSpawned)
        {
            if (candidate == pawn || candidate.Dead || candidate.Downed)
                continue;
            // Protect player-controlled pawns and colony prisoners
            if (candidate.IsColonistPlayerControlled || candidate.IsPrisonerOfColony)
                continue;
            // No friendly fire
            if (candidate.Faction != null && candidate.Faction == pawn.Faction)
                continue;
            // Don't hunt other Necron constructs
            if (candidate.def.GetModExtension<NecronMechExtension>() != null)
                continue;

            float distSq = pawn.Position.DistanceToSquared(candidate.Position);
            if (distSq >= bestDist || distSq > radiusSq)
                continue;
            if (!pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly))
                continue;

            bestDist = distSq;
            best     = candidate;
        }

        return best;
    }
}
