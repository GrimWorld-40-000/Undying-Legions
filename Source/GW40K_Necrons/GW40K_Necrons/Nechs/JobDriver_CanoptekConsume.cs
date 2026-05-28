using System.Collections.Generic;
using NecronGeneUtil;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Canoptek consume job (three phases):
/// (1) Load-for-consume: staging delay + world progress bar, eject paused, stash haulable / despawn plant, compute necro gain.
/// (2) Digest: digestTime = max(5s, necrodermisGain×20); digestIncrement = max(5, round(digestTime)) steps (one pulse per in-game second);
///     each step digestDmg = CurrentHP÷digestIncrement (int) and necro += gain÷digestIncrement; eject enabled (plants still not ejectable per comp).
/// (3) Destruction + end: clear held thing, eject paused, 5s wait before job ends.
/// </summary>
public class JobDriver_CanoptekConsume : JobDriver
{
    /// <summary>5s at 1× tick rate (~60 ticks per real second).</summary>
    private const int StagingTicks = 300;
    private const int EndPhaseTicks = 300;
    /// <summary>RimWorld: one in-game second == 60 ticks at normal speed (same as work seconds).</summary>
    private const int TicksPerGameSecond = 60;
    /// <summary>Digest must run at least this many in-game seconds (never fewer steps).</summary>
    private const int MinDigestSeconds = 5;
    /// <summary>Digest duration in seconds scales as gain×20 (not capped at 5 for everything — only a floor).</summary>
    private const float NecrodermisGainToDigestSeconds = 20f;

    private ThingComp_CanoptekConsumePolicy ConsumeComp =>
        pawn.TryGetComp<ThingComp_CanoptekConsumePolicy>();

    private Thing Subject => job.targetA.Thing;

    private float totalGain;
    private int consumeCount;
    private bool stashedInBuffer;
    private bool delayedPlantPayout;
    private int digestTotalTicks;
    /// <summary>Digest length in seconds (max(5, totalGain×20)); ticks = round(seconds×TPS).</summary>
    private float digestTimeSeconds;
    /// <summary>Tick when the digest delay toil started.</summary>
    private int digestPhaseStartTick = -1;
    /// <summary>Tick when digest phase is allowed to finish.</summary>
    private int digestPhaseEndTick = -1;
    /// <summary>Necro applied during discrete digest steps (remainder in finalize / finish).</summary>
    private float necroAppliedSoFar;
    /// <summary>Hit points or stack count at digest start (plants: 0).</summary>
    private int digestInitialDurability;
    /// <summary>Digest steps (≥ MinDigestSeconds); one pulse per in-game second, one pulse per driver tick max.</summary>
    private int digestIncrement = MinDigestSeconds;
    /// <summary>Absolute game tick for the next digest pulse.</summary>
    private int digestNextPulseTick = -1;
    /// <summary>Integer HP/stack damage per digest step: CurrentHP÷digestIncrement.</summary>
    private int digestDmgPerStep;
    /// <summary>Steps completed (1..digestIncrement).</summary>
    private int digestStepsCompleted;
    /// <summary>HP/stack damage dealt so far (for cumulative rounding).</summary>
    private int digestDamageAppliedCumulative;
    private Thing hiddenPlant;
    private bool consumeCompleted;

    /// <summary>True while the digest toil is applying necrodermis (need-bar gain arrow).</summary>
    public static bool IsDigestingForNecrodermis(Pawn pawn) =>
        pawn?.jobs?.curDriver is JobDriver_CanoptekConsume driver && driver.IsInDigestPhase();

    private bool IsInDigestPhase() =>
        digestPhaseStartTick >= 0
        && digestPhaseEndTick > 0
        && Find.TickManager.TicksGame < digestPhaseEndTick;

    public override bool TryMakePreToilReservations(bool errorOnFailed) =>
        pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);

    public override void ExposeData()
    {
        base.ExposeData();
        int legacyConsumeTicks = 0;
        float legacyTotalGain = 0f;
        float legacyNeApplied = 0f;
        float legacyDmgCarry = 0f;
        bool legacyStashedInBuffer = false;
        int legacyScrapMaterialUnits = 0;
        bool legacyAbsorbEndedEarly = false;
        Scribe_Values.Look(ref legacyConsumeTicks, "consumeTicks", 0);
        Scribe_Values.Look(ref legacyTotalGain, "totalGain", 0f);
        Scribe_Values.Look(ref legacyNeApplied, "neApplied", 0f);
        Scribe_Values.Look(ref legacyDmgCarry, "dmgCarry", 0f);
        Scribe_Values.Look(ref legacyStashedInBuffer, "stashedInBuffer", false);
        Scribe_Values.Look(ref legacyScrapMaterialUnits, "scrapMaterialUnits", 0);
        Scribe_Values.Look(ref legacyAbsorbEndedEarly, "absorbEndedEarly", false);

        Scribe_Values.Look(ref totalGain, "consumeTotalGain", 0f);
        Scribe_Values.Look(ref consumeCount, "consumeCount", 1);
        Scribe_Values.Look(ref stashedInBuffer, "consumeStashedInBuffer", false);
        Scribe_Values.Look(ref delayedPlantPayout, "consumeDelayedPlantPayout", false);
        Scribe_Values.Look(ref digestTotalTicks, "consumeDigestTotalTicks", 0);
        Scribe_Values.Look(ref digestTimeSeconds, "consumeDigestTimeSeconds", 0f);
        Scribe_Values.Look(ref digestPhaseStartTick, "consumeDigestPhaseStartTick", -1);
        Scribe_Values.Look(ref digestPhaseEndTick, "consumeDigestPhaseEndTick", -1);
        Scribe_Values.Look(ref necroAppliedSoFar, "consumeNecroAppliedSoFar", 0f);
        Scribe_Values.Look(ref digestInitialDurability, "consumeDigestInitialDurability", 0);
        Scribe_Values.Look(ref digestIncrement, "consumeDigestIncrement", MinDigestSeconds);
        Scribe_Values.Look(ref digestNextPulseTick, "consumeDigestNextPulseTick", -1);
        Scribe_Values.Look(ref digestDmgPerStep, "consumeDigestDmgPerStep", 0);
        Scribe_Values.Look(ref digestStepsCompleted, "consumeDigestStepsCompleted", 0);
        Scribe_Values.Look(ref digestDamageAppliedCumulative, "consumeDigestDamageAppliedCumulative", 0);
        Scribe_References.Look(ref hiddenPlant, "consumeHiddenPlant");
        Scribe_Values.Look(ref consumeCompleted, "consumeCompleted", false);
    }

    private Thing GetDigestSubject(ThingComp_CanoptekConsumePolicy comp)
    {
        if (stashedInBuffer && comp?.innerContainer != null)
        {
            foreach (Thing held in comp.innerContainer)
            {
                if (held != null && !held.Destroyed)
                    return held;
            }

            return null;
        }

        Thing t = Subject;
        return t != null && !t.Destroyed ? t : null;
    }

    private static void ApplyDurabilityDamage(Thing thing, int amount, Pawn instigator)
    {
        if (thing == null || thing.Destroyed || amount <= 0)
            return;

        if (thing.def.useHitPoints)
        {
            thing.TakeDamage(new DamageInfo(DamageDefOf.Blunt, amount, armorPenetration: 999f, instigator: instigator));
            return;
        }

        int remove = Mathf.Clamp(amount, 0, thing.stackCount);
        for (int i = 0; i < remove && thing.stackCount > 0 && !thing.Destroyed; i++)
        {
            if (thing.stackCount > 1)
                thing.SplitOff(1).Destroy(DestroyMode.Vanish);
            else
                thing.Destroy(DestroyMode.Vanish);
        }
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        ThingComp_CanoptekConsumePolicy comp = ConsumeComp;
        this.FailOn(() => comp == null);
        this.FailOn(() => !NecrodermisIngestionUtility.IsCanoptek(pawn));
        this.AddFinishAction(delegate
        {
            if (!consumeCompleted)
            {
                if (!delayedPlantPayout && totalGain > 1E-5f)
                {
                    float remainder = Mathf.Max(0f, totalGain - necroAppliedSoFar);
                    if (remainder > 1E-5f)
                        NecrodermisIngestionUtility.ApplyNutritionToNecrodermis(pawn, remainder);
                }

                if (comp?.innerContainer != null && comp.innerContainer.Count > 0 && pawn.Spawned && pawn.Map != null)
                {
                    List<Thing> held = new List<Thing>();
                    foreach (Thing t in comp.innerContainer)
                    {
                        if (t != null && !t.Destroyed)
                            held.Add(t);
                    }

                    foreach (Thing t in held)
                    {
                        comp.innerContainer.Remove(t);
                        GenPlace.TryPlaceThing(t, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    }
                }

                if (hiddenPlant != null && !hiddenPlant.Destroyed && !hiddenPlant.Spawned && pawn.Spawned && pawn.Map != null)
                    GenPlace.TryPlaceThing(hiddenPlant, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }

            comp?.EndLoading();
            comp?.SetEjectSuppressed(false);
            hiddenPlant = null;
        });

        Toil gotoTarget = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        gotoTarget.FailOnDestroyedOrNull(TargetIndex.A);
        yield return gotoTarget;

        Toil beginLoading = ToilMaker.MakeToil("CanoptekConsumeBeginLoading");
        beginLoading.initAction = delegate
        {
            comp?.BeginLoading(StagingTicks);
            comp?.SetEjectSuppressed(true);
            job.SetTarget(TargetIndex.B, pawn);
        };
        yield return beginLoading;

        Toil stage = Toils_General.Wait(StagingTicks);
        stage.FailOnDestroyedOrNull(TargetIndex.A);
        stage.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
        stage.WithProgressBarToilDelay(TargetIndex.B);
        yield return stage;

        Toil loadAndPrepareDigest = ToilMaker.MakeToil("CanoptekConsumeLoadAndPrepareDigest");
        loadAndPrepareDigest.FailOnDestroyedOrNull(TargetIndex.A);
        loadAndPrepareDigest.initAction = delegate
        {
            comp?.EndLoading();
            Thing t = Subject;
            if (t == null || t.Destroyed)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            ThingFilter policy = comp.consumeFilter;
            if (policy == null || !policy.Allows(t)
                || !CanoptekConsumePolicyParentFilter.Instance.Allows(t))
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            consumeCount = CanoptekConsumeNecrodermisMath.EffectiveConsumeStackCount(t);
            totalGain = CanoptekConsumeNecrodermisMath.GetNecrodermisGain(t, consumeCount);
            if (totalGain < 1E-4f)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            Need_Necrodermis need = pawn.needs?.TryGetNeed<Need_Necrodermis>();
            if (need == null)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            float remainingCapacity = Mathf.Max(0f, need.MaxLevel - need.CurLevel);
            if (totalGain > remainingCapacity + 1E-4f)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            delayedPlantPayout = t is Plant;
            stashedInBuffer = false;
            necroAppliedSoFar = 0f;
            digestPhaseStartTick = -1;
            digestPhaseEndTick = -1;
            consumeCompleted = false;
            hiddenPlant = null;

            // digestTime (seconds) = max(5, gain×20). At least MinDigestSeconds steps (one pulse per in-game second).
            float rawDigestSeconds = totalGain * NecrodermisGainToDigestSeconds;
            digestTimeSeconds = Mathf.Max(MinDigestSeconds, rawDigestSeconds);
            digestIncrement = Mathf.Max(MinDigestSeconds, Mathf.RoundToInt(digestTimeSeconds));
            digestTotalTicks = digestIncrement * TicksPerGameSecond;
            // Log.Message($"[UL-CanoptekConsume] start pawn={pawn?.LabelShortCap} target={t?.LabelNoCount ?? "null"} gain={totalGain:0.###} digestSec={digestTimeSeconds:0.##} steps={digestIncrement} ticks={digestTotalTicks}");

            if (delayedPlantPayout)
            {
                comp?.SetEjectSuppressed(true);
                hiddenPlant = t;
                if (t.Spawned)
                    t.DeSpawn();
                return;
            }

            comp.innerContainer ??= new ThingOwner<Thing>(comp);
            if (comp.innerContainer.Count > 0)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            bool added = t.Spawned && comp.innerContainer.TryAdd(t, canMergeWithExistingStacks: true);
            if (!added && t.Spawned)
                t.DeSpawn();
            if (!added && !t.Destroyed)
                added = comp.innerContainer.TryAdd(t, canMergeWithExistingStacks: true);

            if (added)
            {
                stashedInBuffer = true;
                // Eject stays paused until digest phase (per design).
                comp?.SetEjectSuppressed(true);
                return;
            }

            if (!t.Destroyed && !t.Spawned && pawn.Map != null)
                GenPlace.TryPlaceThing(t, pawn.Position, pawn.Map, ThingPlaceMode.Near);

            // Some defs may still refuse the buffer even after DeSpawn; digest in place on the map.
            stashedInBuffer = false;
            comp?.SetEjectSuppressed(true);
        };
        yield return loadAndPrepareDigest;

        Toil digest = ToilMaker.MakeToil("CanoptekConsumeDigest");
        digest.defaultCompleteMode = ToilCompleteMode.Never;
        digest.initAction = delegate
        {
            digestPhaseStartTick = Find.TickManager.TicksGame;
            digestPhaseEndTick = digestPhaseStartTick + digestTotalTicks;
            digestNextPulseTick = digestPhaseStartTick + TicksPerGameSecond;
            necroAppliedSoFar = 0f;
            digestStepsCompleted = 0;
            digestDamageAppliedCumulative = 0;

            if (delayedPlantPayout)
            {
                digestInitialDurability = 0;
                digestDmgPerStep = 0;
            }
            else
            {
                Thing sub = GetDigestSubject(comp);
                digestInitialDurability = sub != null && !sub.Destroyed
                    ? (sub.def.useHitPoints ? Mathf.Max(1, sub.HitPoints) : Mathf.Max(1, sub.stackCount))
                    : 1;

                // digestDmg = CurrentHP ÷ digestIncrement (integer division); necro per step = gain ÷ digestIncrement.
                digestDmgPerStep = digestInitialDurability / Mathf.Max(1, digestIncrement);
            }

            comp?.BeginLoading(digestTotalTicks);
            comp?.SetEjectSuppressed(false);
        };
        digest.tickAction = delegate
        {
            if (digestPhaseStartTick < 0 || digestTotalTicks <= 0)
                return;

            int now = Find.TickManager.TicksGame;

            if (delayedPlantPayout)
            {
                // Plants: no HP; one necro slice per in-game second. Only one pulse per tick so seconds actually elapse.
                if (digestStepsCompleted < digestIncrement
                    && now >= digestNextPulseTick)
                {
                    digestNextPulseTick += TicksPerGameSecond;
                    digestStepsCompleted++;
                    float necroStep = totalGain / Mathf.Max(1, digestIncrement);
                    NecrodermisIngestionUtility.ApplyNutritionToNecrodermis(pawn, necroStep);
                    necroAppliedSoFar += necroStep;
                }

                return;
            }

            if (digestInitialDurability <= 0)
                return;

            // One pulse per tick max: otherwise when elapsed first reaches N*60, a while-loop fires every pulse in one frame.
            if (digestStepsCompleted < digestIncrement
                && now >= digestNextPulseTick)
            {
                digestNextPulseTick += TicksPerGameSecond;
                digestStepsCompleted++;
                Thing subject = GetDigestSubject(comp);
                if (subject == null || subject.Destroyed)
                    return;

                float necroStep = totalGain / Mathf.Max(1, digestIncrement);
                NecrodermisIngestionUtility.ApplyNutritionToNecrodermis(pawn, necroStep);
                necroAppliedSoFar += necroStep;

                // digestDmg = CurrentHP÷digestIncrement each step; last step clears any remainder from int division.
                int damageDelta = digestStepsCompleted >= digestIncrement
                    ? Mathf.Max(0, digestInitialDurability - digestDamageAppliedCumulative)
                    : digestDmgPerStep;
                digestDamageAppliedCumulative += damageDelta;
                if (damageDelta > 0)
                    ApplyDurabilityDamage(subject, damageDelta, pawn);
            }

            if (digestPhaseEndTick > 0 && now >= digestPhaseEndTick)
                ReadyForNextToil();
        };
        yield return digest;

        Toil finalize = ToilMaker.MakeToil("CanoptekConsumeFinalize");
        finalize.initAction = delegate
        {
            comp?.SetEjectSuppressed(true);
            comp?.EndLoading();
            if (delayedPlantPayout)
            {
                float plantRemainder = Mathf.Max(0f, totalGain - necroAppliedSoFar);
                if (plantRemainder > 1E-6f)
                    NecrodermisIngestionUtility.ApplyNutritionToNecrodermis(pawn, plantRemainder);
                necroAppliedSoFar = totalGain;
                if (hiddenPlant != null && !hiddenPlant.Destroyed)
                    hiddenPlant.Destroy(DestroyMode.Vanish);
                hiddenPlant = null;
            }
            else
            {
                float remainder = Mathf.Max(0f, totalGain - necroAppliedSoFar);
                if (remainder > 1E-6f)
                {
                    NecrodermisIngestionUtility.ApplyNutritionToNecrodermis(pawn, remainder);
                    necroAppliedSoFar = totalGain;
                }

                Thing t = GetDigestSubject(comp);
                if (stashedInBuffer)
                    comp?.innerContainer?.ClearAndDestroyContents(DestroyMode.Vanish);
                else if (t != null && !t.Destroyed)
                    t.Destroy(DestroyMode.Vanish);
            }
            // Log.Message($"[UL-CanoptekConsume] finalize pawn={pawn?.LabelShortCap} consumed={consumeCompleted} applied={necroAppliedSoFar:0.###}/{totalGain:0.###} steps={digestStepsCompleted}/{digestIncrement}");
            // Let the pawn immediately take a new non-consume job while consume mode itself stays blocked for 5s.
            comp?.BeginPostConsumeReengageDelay(EndPhaseTicks);
            consumeCompleted = true;
        };
        yield return finalize;
    }
}
