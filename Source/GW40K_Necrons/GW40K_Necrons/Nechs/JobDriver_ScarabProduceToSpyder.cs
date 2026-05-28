using System.Collections.Generic;
using NecronGeneUtil;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Moves the scarab to its controlling Spyder and transfers necrodermis into the Hive Fabricator.
/// </summary>
public class JobDriver_ScarabProduceToSpyder : JobDriver
{
    private Pawn Spyder => (Pawn)job.targetA.Thing;

    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.A);
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        // Transfer 20 necrodermis units per second (20 / 60 per tick).
        const float UnitsPerSecond = 20f;
        const float UnitsPerTick = UnitsPerSecond / 60f;

        Toil transfer = ToilMaker.MakeToil("TransferNecrodermis");
        transfer.defaultCompleteMode = ToilCompleteMode.Never;
        transfer.tickAction = () =>
        {
            Pawn spyder = Spyder;
            if (spyder == null || spyder.Dead || spyder.Destroyed) { EndJobWith(JobCondition.Incompletable); return; }

            Need_Necrodermis need = pawn.needs?.TryGetNeed<Need_Necrodermis>();
            HediffComp_HiveFabricator fabricator = GetFabricator(spyder);

            if (need == null || fabricator == null || need.CurLevel <= 0f || fabricator.stored >= fabricator.Props.maxStored)
            {
                EndJobWith(JobCondition.Succeeded);
                return;
            }

            float unitsThisTick = System.Math.Min(UnitsPerTick, fabricator.Props.maxStored - fabricator.stored);
            float needDrain = unitsThisTick / fabricator.Props.necrodermisUnitsPerNeedLevel;
            needDrain = System.Math.Min(needDrain, need.CurLevel);
            unitsThisTick = needDrain * fabricator.Props.necrodermisUnitsPerNeedLevel;

            fabricator.AddNecrodermis(unitsThisTick);
            need.CurLevel -= needDrain;

            if (need.CurLevel <= 0f || fabricator.stored >= fabricator.Props.maxStored)
                EndJobWith(JobCondition.Succeeded);
        };
        yield return transfer;
    }

    private static HediffComp_HiveFabricator GetFabricator(Pawn spyder)
    {
        HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("GW40K_HiveFabricator");
        return def == null ? null : spyder.health.hediffSet.GetFirstHediffOfDef(def)?.TryGetComp<HediffComp_HiveFabricator>();
    }
}
