using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Before a hostile pawn exits the map, checks for nearby downed Necrons.
/// Skipped for morale-break retreats (ExitMapBestAndDefendSelf duty), mental states (panic flee),
/// and when a living threat is within 6 tiles of the downed Necron.
///
/// Necron siege lords are excluded entirely — their dedicated <see cref="LordToil_NecronFinishOff"/>
/// phase (between Final Assault and Withdraw) handles finish-off inside the lord graph so it
/// cannot interfere with the steal / kidnap / exit outcome chain.
/// </summary>
[HarmonyPatch(typeof(JobGiver_ExitMap), "TryGiveJob")]
public static class HarmonyPatch_FinishOffNecronBeforeLeaving
{
    private static int lastNotificationTick = -99999;

    [HarmonyPrefix]
    public static bool Prefix(Pawn pawn, ref Job __result)
    {
        if (pawn.Map == null || pawn.Faction == null || !pawn.Faction.HostileTo(Faction.OfPlayer))
            return true;

        // Skip for individual panic/flee mental states (e.g. PanicFlee)
        if (pawn.InMentalState)
            return true;

        // Skip for any flee/panic retreat duty — morale break (ExitMapBestAndDefendSelf) or random panic flee (ExitMapRandom)
        string dutyDef = pawn.mindState?.duty?.def?.defName;
        if (dutyDef == "ExitMapBestAndDefendSelf" || dutyDef == "ExitMapRandom")
            return true;

        // Necron siege lords manage finish-off via LordToil_NecronFinishOff, inserted
        // between Final Assault and Withdraw in the lord graph.
        // Forfeit the exit intercept entirely so the lord's outcome chain runs unimpeded.
        if (pawn.GetLord()?.LordJob is LordJob_NecronSiege)
            return true;

        Pawn target = FindNearestDownedNecron(pawn);
        if (target == null)
            return true;

        // Skip if any living player-faction pawn is within 6 tiles of the Necron
        if (HasNearbyThreat(target))
            return true;

        if (Find.TickManager.TicksGame - lastNotificationTick > 2500)
        {
            Messages.Message(
                "Raiders are finishing off your Necrons before leaving.",
                target,
                MessageTypeDefOf.ThreatSmall);
            lastNotificationTick = Find.TickManager.TicksGame;
        }

        __result = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("GW40K_Job_FinishOffNecron"), target);
        return false;
    }

    private static Pawn FindNearestDownedNecron(Pawn raider)
    {
        return (Pawn)GenClosest.ClosestThingReachable(
            raider.Position,
            raider.Map,
            ThingRequest.ForGroup(ThingRequestGroup.Pawn),
            PathEndMode.Touch,
            TraverseParms.For(raider),
            maxDistance: 20f,
            validator: t => t is Pawn p
                && p.Downed
                && !p.Dead
                && p.Faction == Faction.OfPlayer
                && p.def.GetModExtension<NonOrganicPawn>() != null);
    }

    private static bool HasNearbyThreat(Pawn necron)
    {
        foreach (Pawn p in necron.Map.mapPawns.AllPawnsSpawned)
        {
            if (p.Dead || p.Downed) continue;
            if (p.Faction != Faction.OfPlayer) continue;
            if (p.Position.DistanceTo(necron.Position) <= 6f) return true;
        }
        return false;
    }
}
