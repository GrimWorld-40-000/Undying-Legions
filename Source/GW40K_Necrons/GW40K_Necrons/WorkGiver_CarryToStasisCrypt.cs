using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Automatically carries downed Necrons in forced Eternal Slumber to a valid stasis crypt.
/// Scoped to the emergency state so vanilla rescue/bed behavior remains unchanged otherwise.
/// </summary>
public class WorkGiver_CarryToStasisCrypt : WorkGiver_Scanner
{
    public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        if (pawn?.Map == null)
            yield break;

        List<Thing> allPawns = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn);
        for (int i = 0; i < allPawns.Count; i++)
            yield return allPawns[i];
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (pawn == null || t is not Pawn sleeper)
            return false;
        if (sleeper == pawn || sleeper.Dead || !sleeper.Downed)
            return false;
        if (pawn.InMentalState || pawn.Downed)
            return false;
        if (sleeper.health?.hediffSet?.GetFirstHediffOfDef(NecronDefOfs.GW40K_EternalSlumberForced) == null)
            return false;
        if (!pawn.CanReserveAndReach(sleeper, PathEndMode.Touch, Danger.Deadly))
            return false;

        NecronCasket casket = NecroCasketUtility.FindNecroCasket(
            sleeper,
            pawn,
            checkSocialProperness: true,
            ignoreOtherReservations: false,
            guestStatus: sleeper.GuestStatus);
        return casket != null;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (t is not Pawn sleeper)
            return null;

        NecronCasket casket = NecroCasketUtility.FindNecroCasket(
            sleeper,
            pawn,
            checkSocialProperness: true,
            ignoreOtherReservations: false,
            guestStatus: sleeper.GuestStatus);
        if (casket == null)
            return null;

        // CarryToCryptosleepCasket expects count >= 1; default Job count is -1 (vanilla logs "Invalid count: -1").
        Job job = JobMaker.MakeJob(JobDefOf.CarryToCryptosleepCasket, sleeper, casket);
        job.count = 1;
        return job;
    }
}
