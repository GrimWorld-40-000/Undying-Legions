using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Pawn walks to a power battery and stands there charging gauss energy.
/// 4 Wd of battery per 0.01 gauss (400 Wd for a full 0→1 bar) — same efficiency as before, scaled to rate.
/// Rate: ~10% bar per real-time second at 1× speed (~60 ticks/s), so a full bar is ~10s of standing charge.
/// Job ends when gauss reaches 95% or the battery runs dry.
/// </summary>
public class JobDriver_RechargeFromBattery : JobDriver
{
    /// <summary>400 Wd drawn from the battery per 1.0 gauss filled (4 Wd per 1% of bar).</summary>
    private const float WdPerGaussUnit = 400f;

    /// <summary>RimWorld advances ~60 simulation ticks per real-time second at speed 1×.</summary>
    private const float TicksPerRealSecond = 60f;

    /// <summary>Fraction of gauss bar (0–1) gained per real-time second while charging.</summary>
    private const float GaussFractionPerRealSecond = 0.10f;

    private const float GaussGainPerTick = GaussFractionPerRealSecond / TicksPerRealSecond;
    private const float BatteryDrainPerTick = GaussGainPerTick * WdPerGaussUnit;
    private const float StopThreshold = 0.95f;

    private CompPowerBattery Battery => job.targetA.Thing?.TryGetComp<CompPowerBattery>();

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.A);

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

        Toil charge = ToilMaker.MakeToil("GaussRechargeFromBattery");
        charge.tickAction = () =>
        {
            CompPowerBattery battery = Battery;
            if (battery == null || battery.StoredEnergy < BatteryDrainPerTick)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            Need_NechEnergy gauss = pawn.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy) as Need_NechEnergy;
            if (gauss == null || gauss.CurLevel >= StopThreshold)
            {
                EndJobWith(JobCondition.Succeeded);
                return;
            }

            battery.DrawPower(BatteryDrainPerTick);
            gauss.CurLevel += GaussGainPerTick;
        };
        charge.defaultCompleteMode = ToilCompleteMode.Never;
        charge.handlingFacing = false;
        charge.socialMode = RandomSocialMode.Off;
        yield return charge;
    }
}
