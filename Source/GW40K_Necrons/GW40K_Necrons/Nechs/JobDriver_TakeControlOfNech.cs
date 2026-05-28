using System.Collections.Generic;
using System;
using NecronGeneUtil;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

public class JobDriver_TakeControlOfNech : JobDriver
{
    private const int BaseTicksPerBandwidth = 600;
    private const float FailChancePerBandwidth = 0.08f;
    private const TargetIndex TargetNechInd = TargetIndex.A;
    private const string ControlPulseMoteDefName = "Mote_GW40K_MechUncontrolled_Nech";

    private Effecter controlEffecter;

    private Pawn TargetNech => job.GetTarget(TargetNechInd).Pawn;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        Pawn target = TargetNech;
        if (target == null)
            return false;
        return pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.AddFinishAction(delegate { CleanupControlEffecter(); });

        this.FailOnDespawnedNullOrForbidden(TargetNechInd);
        this.FailOn(() => TargetNech == null || HediffComp_NecronCommandTracker.GetCommanderOf(TargetNech) != null);
        this.FailOn(() => TargetNech != null && !NechTakeControlJobUtility.IsFriendlyTakeControlTarget(pawn, TargetNech));
        this.FailOn(() => NechTakeControlJobUtility.TakeControlTargetNoLongerValid(TargetNech, pawn));

        yield return Toils_Goto.GotoThing(TargetNechInd, PathEndMode.Touch);

        Toil control = Toils_General.Wait(ControlDurationTicks());
        control.WithProgressBarToilDelay(TargetNechInd);
        control.FailOnCannotTouch(TargetNechInd, PathEndMode.Touch);
        control.initAction = delegate
        {
            Pawn target = TargetNech;
            if (target != null && pawn.Spawned && target.Spawned && pawn.MapHeld == target.MapHeld)
            {
                EffecterDef fx = DefDatabase<EffecterDef>.GetNamedSilentFail("ControlMech");
                if (fx != null)
                    controlEffecter = fx.Spawn(new TargetInfo(pawn), new TargetInfo(target), 1f);
                else
                    SpawnControlPulse(target);
            }
        };
        control.tickAction = delegate
        {
            Pawn target = TargetNech;
            if (target == null || HediffComp_NecronCommandTracker.GetCommanderOf(target) != null)
            {
                ReadyForNextToil();
                return;
            }

            // Keep the target stationary while command takeover is in progress.
            target.pather?.StopDead();

            if (pawn.rotationTracker != null && target.Spawned && pawn.MapHeld == target.MapHeld)
                pawn.rotationTracker.FaceCell(target.Position);

            controlEffecter?.EffectTick(new TargetInfo(pawn), new TargetInfo(target));
        };
        control.AddFinishAction(CleanupControlEffecter);
        control.AddFinishAction(delegate
        {
            Pawn target = TargetNech;
            if (target != null && target.Spawned)
                target.jobs?.EndCurrentJob(JobCondition.InterruptForced, true);
        });
        yield return control;

        yield return Toils_General.Do(BindTargetNech);
    }

    private void CleanupControlEffecter()
    {
        if (controlEffecter == null)
            return;
        controlEffecter.Cleanup();
        controlEffecter = null;
    }

    private int ControlDurationTicks()
    {
        Pawn target = TargetNech;
        HediffComp_NecronCommandTracker tracker = HediffComp_NecronCommandTracker.GetTracker(pawn);
        if (target == null || tracker == null || target.kindDef == null)
            return BaseTicksPerBandwidth;

        float cost = tracker.CommandBandwidthCostForPawnKind(target.kindDef);
        return GenMath.RoundRandom(Math.Max(1f, cost * BaseTicksPerBandwidth));
    }

    private void BindTargetNech()
    {
        Pawn target = TargetNech;
        HediffComp_NecronCommandTracker tracker = HediffComp_NecronCommandTracker.GetTracker(pawn);
        if (target == null)
            return;
        if (tracker == null)
            return;
        if (HediffComp_NecronCommandTracker.GetCommanderOf(target) != null)
            return;
        if (!tracker.HasBandwidthFor(target))
        {
            Messages.Message("GW40K_CommandBandwidthFull".Translate(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        if (!NechTakeControlJobUtility.IsFriendlyTakeControlTarget(pawn, target))
        {
            Messages.Message("GW40K_TakeControlNotFriendly".Translate(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        float failChance = ControlFailChance(tracker, target);
        if (Rand.Chance(failChance))
        {
            Messages.Message(RandomFailureMessage().Translate(), MessageTypeDefOf.RejectInput, true);
            return;
        }

        tracker.BindMech(target);

        // On take-control, run the same auto-mode decision as TickAutoMode so a newly
        // bound scarab with full necro doesn't idle in Consume mode.
        GameComponent_CanoptekConstructModes modes = GameComponent_CanoptekConstructModes.Current;
        if (modes != null && modes.GetMode(target) == ControlNodeMode.Consume)
        {
            Need_Necrodermis necroNeed = target.needs?.TryGetNeed<Need_Necrodermis>();
            if (necroNeed != null && necroNeed.CurLevelPercentage > 0.95f)
            {
                bool hasRepair = JobGiver_CanoptekRepair.HasAnyRepairTarget(target);
                modes.SetMode(target, hasRepair ? ControlNodeMode.Repair : ControlNodeMode.Work);
            }
        }

        SoundDef complete = DefDatabase<SoundDef>.GetNamedSilentFail("ControlMech_Complete");
        if (complete != null && target.MapHeld != null)
            complete.PlayOneShot(SoundInfo.InMap(target));
        else if (target.MapHeld != null)
            SoundDefOf.Tick_Tiny.PlayOneShot(SoundInfo.InMap(target));
        Messages.Message("GW40K_TakeCommandSuccess".Translate(pawn.LabelShortCap, target.LabelShortCap), MessageTypeDefOf.PositiveEvent, true);
    }

    private static float ControlFailChance(HediffComp_NecronCommandTracker tracker, Pawn target)
    {
        if (tracker == null || target?.kindDef == null)
            return 0f;
        float cost = tracker.CommandBandwidthCostForPawnKind(target.kindDef);
        return Math.Max(0f, Math.Min(1f, cost * FailChancePerBandwidth));
    }

    private static string RandomFailureMessage()
    {
        switch (Rand.RangeInclusive(1, 4))
        {
            case 1: return "GW40K_TakeCommandFailedLockout";
            case 2: return "GW40K_TakeCommandFailedHandshake";
            case 3: return "GW40K_TakeCommandFailedInterference";
            default: return "GW40K_TakeCommandFailedSignalLoss";
        }
    }

    private static void SpawnControlPulse(Pawn target)
    {
        ThingDef mote = DefDatabase<ThingDef>.GetNamedSilentFail(ControlPulseMoteDefName);
        if (mote != null)
            MoteMaker.MakeAttachedOverlay(target, mote, UnityEngine.Vector3.zero);
        ThingDef interactMote = DefDatabase<ThingDef>.GetNamedSilentFail("Mote_Interact");
        if (interactMote != null)
            MoteMaker.MakeAttachedOverlay(target, interactMote, UnityEngine.Vector3.zero);
    }
}
