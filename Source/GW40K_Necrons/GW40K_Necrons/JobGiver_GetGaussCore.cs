using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Auto-uses a gauss core when gauss energy is low.
/// Prevents Necron gauss users from drifting into vanilla meal-seeking paths.
/// </summary>
public class JobGiver_GetGaussCore : ThinkNode_JobGiver
{
    private const float TriggerThreshold = 0.10f;
    private static ThingDef _gaussCoreDef;
    private static JobDef _consumeJobDef;

    private static ThingDef GaussCoreDef =>
        _gaussCoreDef ??= DefDatabase<ThingDef>.GetNamedSilentFail("GW40K_GaussCore");

    private static JobDef ConsumeJobDef =>
        _consumeJobDef ??= DefDatabase<JobDef>.GetNamedSilentFail("GW40K_Job_ConsumeGaussCore");

    public override float GetPriority(Pawn pawn)
    {
        if (!ShouldSeekCore(pawn))
            return 0f;
        return 9.6f;
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!ShouldSeekCore(pawn))
            return null;
        if (ConsumeJobDef != null && (pawn.CurJobDef == ConsumeJobDef || pawn.jobs?.curDriver is JobDriver_ConsumeGaussCore))
            return null;

        JobDef jobDef = ConsumeJobDef;
        if (jobDef == null)
            return null;

        Thing core = GetBestCore(pawn);
        if (core == null)
            return null;

        return JobMaker.MakeJob(jobDef, core);
    }

    private static bool ShouldSeekCore(Pawn pawn)
    {
        if (pawn == null)
            return false;
        if (GaussCoreDef == null)
            return false;
        if (NechEnergyUtility.GetCapacitorComp(pawn) == null)
            return false;
        // Auto-seeking only makes sense while a gauss weapon is equipped.
        // Pawns with a capacitor but no weapon can still manually siphon cores
        // via the right-click float menu, but the AI should not drag them to cores unprompted.
        if (!GaussWeaponUtil.HasEquippedGaussWeapon(pawn))
            return false;
        if (!NechEnergyUtility.AllowAutoConsume(pawn))
            return false;

        Need_NechEnergy gauss = pawn.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy) as Need_NechEnergy;
        if (gauss == null)
            return false;
        if (gauss.CurLevelPercentage >= TriggerThreshold)
            return false;

        return true;
    }

    private static Thing GetBestCore(Pawn pawn)
    {
        ThingDef coreDef = GaussCoreDef;
        if (coreDef == null)
            return null;

        Thing carried = pawn.carryTracker?.CarriedThing;
        if (IsUsableCoreFor(pawn, carried, coreDef))
            return carried;

        if (pawn.inventory?.innerContainer != null)
        {
            for (int i = 0; i < pawn.inventory.innerContainer.Count; i++)
            {
                Thing t = pawn.inventory.innerContainer[i];
                if (IsUsableCoreFor(pawn, t, coreDef))
                    return t;
            }
        }

        if (!pawn.Spawned || pawn.Map == null)
            return null;

        return GenClosest.ClosestThing_Global_Reachable(
            pawn.Position,
            pawn.Map,
            pawn.Map.listerThings.ThingsOfDef(coreDef),
            PathEndMode.ClosestTouch,
            TraverseParms.For(pawn, Danger.Deadly),
            9999f,
            t => IsUsableCoreFor(pawn, t, coreDef) && pawn.CanReserve(t));
    }

    private static bool IsUsableCoreFor(Pawn pawn, Thing thing, ThingDef coreDef)
    {
        if (thing == null || thing.def != coreDef)
            return false;
        if (thing.IsForbidden(pawn))
            return false;

        CompUsable usable = thing.TryGetComp<CompUsable>();
        if (usable == null)
            return false;
        return usable.CanBeUsedBy(pawn).Accepted;
    }
}
