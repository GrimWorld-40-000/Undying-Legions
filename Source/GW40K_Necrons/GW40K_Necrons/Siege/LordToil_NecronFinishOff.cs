using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Siege Phase 4.5 — inserted between Final Assault and Withdraw.
///
/// Raiders attempt to destroy downed / reanimating Necrons before the withdrawal
/// outcome chain (steal / kidnap / exit) begins. Runs for up to 30 seconds, or
/// until no valid targets remain, whichever comes first.
///
/// Transitions are driven by <see cref="AnyTargetsRemain"/> and a hard
/// <see cref="Trigger_TicksPassed"/> cap declared in <see cref="LordJob_NecronSiege"/>.
/// </summary>
public class LordToil_NecronFinishOff : LordToil
{
    private static readonly int TickInterval = GenTicks.TicksPerRealSecond; // check every second

    private static JobDef _finishOffJob;
    private static JobDef FinishOffJob =>
        _finishOffJob ??= DefDatabase<JobDef>.GetNamedSilentFail("GW40K_Job_FinishOffNecron");

    private static DutyDef _scarabDuty;
    private static DutyDef ScarabDuty =>
        _scarabDuty ??= DefDatabase<DutyDef>.GetNamedSilentFail("GW_UL_ScarabAssault") ?? DutyDefOf.Defend;

    // ── Duties ───────────────────────────────────────────────────────────────

    public override void UpdateAllDuties()
    {
        // Keep raiders in active assault mode so they engage any standing enemies
        // while the toil tick simultaneously redirects idle ones to downed Necrons.
        IntVec3 target = RaidStrategyWorker_NecronSiege.FindColonyCenter(Map);
        LordToil_NecronSiegeAssault.AssignNecronAssaultDuties(
            lord, target, DutyDefOf.AssaultColony, ScarabDuty);
    }

    // ── Per-tick job dispatch ─────────────────────────────────────────────────

    public override void LordToilTick()
    {
        if (Find.TickManager.TicksGame % TickInterval != 0) return;

        JobDef finishJob = FinishOffJob;
        if (finishJob == null) return;

        foreach (Pawn raider in lord.ownedPawns)
        {
            if (raider == null || raider.Dead || !raider.Spawned || raider.Downed) continue;
            if (raider.InMentalState) continue;

            // Don't interrupt an in-progress finish-off or active melee/ranged attack.
            JobDef cur = raider.CurJobDef;
            if (cur == finishJob
                || cur == JobDefOf.AttackMelee
                || cur == JobDefOf.AttackStatic)
                continue;

            Pawn necron = FindNearestDownedNecron(raider);
            if (necron == null) continue;
            if (HasNearbyThreat(necron)) continue;

            raider.jobs?.StartJob(
                JobMaker.MakeJob(finishJob, necron),
                JobCondition.InterruptForced,
                null,
                resumeCurJobAfterwards: false,
                cancelBusyStances: false);
        }
    }

    // ── Transition helper ─────────────────────────────────────────────────────

    /// <summary>
    /// True while at least one downed, living player-faction Necron remains on the map.
    /// Used by the <see cref="Trigger_TickCondition"/> in <see cref="LordJob_NecronSiege"/>
    /// to advance to the withdraw dispatch once the cleanup is done.
    /// </summary>
    public static bool AnyTargetsRemain(Map map)
    {
        if (map == null) return false;
        foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
        {
            if (p.Faction != Faction.OfPlayer || p.Dead || !p.Downed) continue;
            if (p.def.GetModExtension<NonOrganicPawn>() != null)
                return true;
        }
        return false;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static Pawn FindNearestDownedNecron(Pawn raider) =>
        (Pawn)GenClosest.ClosestThingReachable(
            raider.Position,
            raider.Map,
            ThingRequest.ForGroup(ThingRequestGroup.Pawn),
            PathEndMode.Touch,
            TraverseParms.For(raider),
            maxDistance: 30f,
            validator: t => t is Pawn p
                && p.Downed
                && !p.Dead
                && p.Faction == Faction.OfPlayer
                && p.def.GetModExtension<NonOrganicPawn>() != null);

    private static bool HasNearbyThreat(Pawn necron)
    {
        foreach (Pawn p in necron.Map.mapPawns.AllPawnsSpawned)
        {
            if (p.Dead || p.Downed || p.Faction != Faction.OfPlayer) continue;
            if (p.Position.DistanceTo(necron.Position) <= 6f) return true;
        }
        return false;
    }
}
