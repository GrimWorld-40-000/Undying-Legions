using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Consumes a gauss core from the pawn's inventory or from the ground.
/// Replaces UseItem for gauss cores because UseItem's FailOnDespawned condition
/// silently aborts when the item is in inventory (not spawned on the map).
/// </summary>
public class JobDriver_ConsumeGaussCore : JobDriver
{
    /// <summary>Siphon delay at 1× speed (60 ticks ≈ 1 real second).</summary>
    private const int UseDurationTicks = 480; // 8s

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        Thing thing = job.GetTarget(TargetIndex.A).Thing;
        // Inventory items are already "owned" — no map reservation needed.
        if (thing != null && !thing.Spawned)
            return true;
        return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.A);
        this.FailOn(() => pawn.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy) == null);
        this.FailOn(() => NechEnergyUtility.GetCapacitorComp(pawn) == null);

        // If the core is forbidden (player ordered via right-click on a red-X core),
        // unforbid it so the pawn can actually pick it up.
        Toil unforbid = ToilMaker.MakeToil("UnforbidGaussCore");
        unforbid.defaultCompleteMode = ToilCompleteMode.Instant;
        unforbid.initAction = () =>
        {
            Thing core = job.GetTarget(TargetIndex.A).Thing;
            if (core != null && core.Spawned && core.IsForbidden(pawn))
                core.SetForbidden(false, false);
        };
        yield return unforbid;

        Thing target = job.GetTarget(TargetIndex.A).Thing;
        if (target != null && target.Spawned)
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        Toil use = ToilMaker.MakeToil("ConsumeGaussCore_Use");
        use.defaultDuration = UseDurationTicks;
        use.defaultCompleteMode = ToilCompleteMode.Delay;
        use.handlingFacing = true;
        use.socialMode = RandomSocialMode.Off;
        use.WithProgressBar(TargetIndex.A,
            () => 1f - (float)(use.actor?.jobs?.curDriver?.ticksLeftThisToil ?? 0) / UseDurationTicks,
            interpolateBetweenActorAndTarget: false);
        yield return use;

        Toil apply = ToilMaker.MakeToil("ConsumeGaussCore_Apply");
        apply.initAction = delegate
        {
            if (job.GetTarget(TargetIndex.A).Thing is not ThingWithComps core || core.Destroyed) return;
            foreach (CompUseEffect eff in core.GetComps<CompUseEffect>())
                eff.DoEffect(pawn);
        };
        apply.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return apply;
    }
}
