using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Sends a Necron to recharge gauss energy from a nearby power battery when
/// the toggle is enabled and gauss drops to 50% or below.
/// </summary>
public class JobGiver_GaussRechargeFromBattery : ThinkNode_JobGiver
{
    private const float TriggerThreshold = 0.50f;
    private const float MaxRange = 20f;

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!NechEnergyUtility.AllowBatteryCharge(pawn))
            return null;

        Need_NechEnergy gauss = pawn.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy) as Need_NechEnergy;
        if (gauss == null || gauss.CurLevelPercentage > TriggerThreshold)
            return null;

        CompPowerBattery battery = FindBattery(pawn);
        if (battery == null)
            return null;

        return JobMaker.MakeJob(NecronDefOfs.GW40K_Job_RechargeFromBattery, battery.parent);
    }

    private static CompPowerBattery FindBattery(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Map == null)
            return null;

        CompPowerBattery best = null;
        float bestDist = float.MaxValue;
        foreach (Building b in pawn.Map.listerBuildings.allBuildingsColonist)
        {
            CompPowerBattery c = b?.GetComp<CompPowerBattery>();
            if (c == null || c.StoredEnergy < 10f)
                continue;
            float d = b.Position.DistanceTo(pawn.Position);
            if (d > MaxRange || d >= bestDist)
                continue;
            if (!pawn.CanReach(b, PathEndMode.InteractionCell, Danger.None))
                continue;
            best = c;
            bestDist = d;
        }
        return best;
    }
}
