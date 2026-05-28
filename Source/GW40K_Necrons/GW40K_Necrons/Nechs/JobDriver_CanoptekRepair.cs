using System.Collections.Generic;
using NecronGeneUtil;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Consumes technician necrodermis to mend scarabs, ally Necrons, and friendly buildings.
/// Self-repair scarab units; allied repair uses separate body-part/injury paths per race.
/// </summary>
public class JobDriver_CanoptekRepair : JobDriver
{
    /// <summary>
    /// Missing-body-part restore (scarab unit, ally necron limb, etc.): one-shot cost and minimum
    /// <see cref="Need_Necrodermis.CurLevel"/> before the repair job may start (do not initiate under ~34%).
    /// </summary>
    internal const float SelfRepairNecroPerPart = 0.34f;

    /// <summary>Used with <see cref="SelfScarabInjuryHealHpPerGameSecond"/> to convert necro spent into heal severity (HP per necro).</summary>
    internal const float SelfScarabInjuryNecroPerGameSecond = 0.033f;

    private const int SelfRepairSecondsPerCycle = 10;
    private const int SelfMissingScarabPartRepairSeconds = 20;
    private const int TicksPerGameSecond = 60;
    /// <summary>Necro spent per 10s injury-repair cycle (one injury step per cycle).</summary>
    private const float SelfScarabInjuryNecroPerRepairCycle = 0.065f;
    /// <summary>HP (injury severity) healed per in-game second on GW40K_ScarabUnit parts while necro lasts.</summary>
    private const float SelfScarabInjuryHealHpPerGameSecond = 21.5f;

    /// <summary>Injury-severity pooled across non–scarab-unit limbs per ally repair pulse (spread via <see cref="NecronStasisHealing.ApplyHealPulse"/>).</summary>
    private const float AllyNecronInjuryHealSeverityPerHealCycle = 85f;

    private const int OtherStructureRepairSeconds = 10;
    private const int NecronStructureRepairSeconds = 5;
    private const float OtherStructureHpPercentPerNecroPercent = 1f;
    private const float NecronStructureHpPercentPerNecroPercent = 2f;

    private Thing TargetThing => job.targetA.Thing;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if (TargetThing == null || TargetThing == pawn)
            return true;
        return pawn.Reserve(TargetThing, job, errorOnFailed: errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        Thing target = TargetThing;
        bool selfTarget = target == pawn;
        bool targetIsAllyPawn = !selfTarget && target is Pawn ally && ally != pawn;
        bool necronStructureTarget = !selfTarget && target is Thing tns && !(tns is Pawn) && IsNecronStructureBuilding(tns);

        this.FailOn(() => TargetThing == null || TargetThing.Destroyed);

        Toil init = ToilMaker.MakeToil("CanoptekRepairInit");
        init.initAction = delegate
        {
            // Building repair only — pawn targets bypass structure-duration setup.
            if (!selfTarget && !targetIsAllyPawn)
            {
                int durationTicks = GetStructureRepairDurationTicks(necronStructureTarget);
                if (durationTicks <= 0)
                    EndJobWith(JobCondition.Incompletable);
            }

            job.SetTarget(TargetIndex.B, pawn);
        };
        yield return init;

        if (selfTarget)
        {
            bool hadInjuryAtStart = HasInjuredScarabUnitParts(pawn);
            bool hadMissingAtStart = HasMissingScarabPart(pawn);
            if (hadInjuryAtStart)
            {
                foreach (Toil tl in MakeScarabInjuryRepairSequence(pawn, pawn))
                    yield return tl;
            }
            if (hadMissingAtStart)
            {
                foreach (Toil tl in MakeMissingScarabRestoreSequence(pawn, pawn))
                    yield return tl;
            }

            yield break;
        }

        if (targetIsAllyPawn && target is Pawn alliedRepairTarget)
        {
            foreach (Toil allyToil in MakeFriendlyPawnRepairToils(alliedRepairTarget))
                yield return allyToil;

            yield break;
        }

        Toil goToTarget = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        goToTarget.FailOnDestroyedOrNull(TargetIndex.A);
        yield return goToTarget;

        int structureTicks = GetStructureRepairDurationTicks(necronStructureTarget);
        Toil wait = Toils_General.Wait(Mathf.Max(1, structureTicks));
        wait.WithProgressBarToilDelay(TargetIndex.B);
        wait.FailOnDestroyedOrNull(TargetIndex.A);
        yield return wait;

        Toil apply = ToilMaker.MakeToil("CanoptekRepairApply");
        apply.initAction = delegate { ApplyStructureRepair(pawn, TargetThing, IsNecronStructureBuilding(TargetThing)); };
        yield return apply;
    }

    /// <summary>Another friendly Necron (or scarab swarm) — Goto + optional repair pulses.</summary>
    private IEnumerable<Toil> MakeFriendlyPawnRepairToils(Pawn patient)
    {
        Toil goTo = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        goTo.FailOnDestroyedOrNull(TargetIndex.A);
        yield return goTo;

        bool scarabPatient = HarmonyPatch_ScarabSwarmChassis.IsScarabSwarm(patient);

        if (scarabPatient)
        {
            if (HasInjuredScarabUnitParts(patient))
                foreach (Toil t in MakeScarabInjuryRepairSequence(pawn, patient))
                    yield return t;
            if (HasMissingScarabPart(patient))
                foreach (Toil t in MakeMissingScarabRestoreSequence(pawn, patient))
                    yield return t;
            yield break;
        }

        if (NechEnergyUtility.IsNecronPawn(patient))
        {
            if (FindNextGeneralMissingPartForNecronAlly(patient) != null)
                foreach (Toil t in MakeAllyGeneralMissingRestoreSequence(pawn, patient))
                    yield return t;

            if (NecronStasisHealing.SumHealableInjurySeverity(patient) > 0.001f)
                foreach (Toil t in MakeAllyGeneralInjuryRepairSequence(pawn, patient))
                    yield return t;

            yield break;
        }

        if (!patient.RaceProps.IsMechanoid)
            yield break;

        if (FindNextGeneralMissingPartForNecronAlly(patient) != null)
            foreach (Toil t in MakeAllyGeneralMissingRestoreSequence(pawn, patient))
                yield return t;

        if (NecronStasisHealing.SumHealableInjurySeverity(patient) > 0.001f)
            foreach (Toil t in MakeAllyGeneralInjuryRepairSequence(pawn, patient))
                yield return t;
    }

    private IEnumerable<Toil> MakeAllyGeneralMissingRestoreSequence(Pawn technician, Pawn patient)
    {
        yield return MakePausedContactWaitToil(
            technician,
            patient,
            SelfMissingScarabPartRepairSeconds * TicksPerGameSecond,
            "CanoptekRepairWaitAllyMissing");

        Toil applyMissing = ToilMaker.MakeToil("CanoptekRepairApplyAllyGeneralMissing");
        applyMissing.initAction = delegate { ApplyGeneralMissingPartRestoreAlly(technician, patient); };
        yield return applyMissing;
    }

    private IEnumerable<Toil> MakeAllyGeneralInjuryRepairSequence(Pawn technician, Pawn patient)
    {
        yield return MakePausedContactWaitToil(
            technician,
            patient,
            SelfRepairSecondsPerCycle * TicksPerGameSecond,
            "CanoptekRepairWaitAllyInjury");

        Toil apply = ToilMaker.MakeToil("CanoptekRepairApplyAllyGeneralInjury");
        apply.initAction = delegate
        {
            Need_Necrodermis need = technician.needs?.TryGetNeed<Need_Necrodermis>();
            if (need == null || need.CurLevel <= 1e-5f)
                return;

            float necroSpend = Mathf.Min(SelfScarabInjuryNecroPerRepairCycle, need.CurLevel);
            float healPoints = necroSpend * AllyNecronInjuryHealSeverityPerHealCycle;
            if (healPoints <= 1e-5f)
                return;

            NecronStasisHealing.ApplyHealPulse(patient, healPoints);
            ConsumeNecrodermis(need, necroSpend);
        };
        yield return apply;
    }

    /// <summary>
    /// Evaluated after any self injury phase so wait length and necro gate reflect current state.
    /// </summary>
    private IEnumerable<Toil> MakeMissingScarabRestoreSequence(Pawn technician, Pawn patient)
    {
        Need_Necrodermis need = technician.needs?.TryGetNeed<Need_Necrodermis>();
        int waitTicks = HasMissingScarabPart(patient) && need != null && need.CurLevel >= SelfRepairNecroPerPart - 1e-4f
            ? SelfMissingScarabPartRepairSeconds * TicksPerGameSecond
            : 0;
        if (waitTicks > 0)
        {
            if (technician == patient)
            {
                Toil waitMissing = Toils_General.Wait(waitTicks);
                waitMissing.WithProgressBarToilDelay(TargetIndex.B);
                waitMissing.FailOnDestroyedOrNull(TargetIndex.A);
                yield return waitMissing;
            }
            else
            {
                yield return MakePausedContactWaitToil(
                    technician,
                    patient,
                    waitTicks,
                    "CanoptekRepairWaitScarabMissing");
            }
        }

        Toil applyMissing = ToilMaker.MakeToil("CanoptekRepairApplyMissingScarab");
        applyMissing.initAction = delegate { ApplyMissingScarabPartRestore(technician, patient); };
        yield return applyMissing;
    }

    private IEnumerable<Toil> MakeScarabInjuryRepairSequence(Pawn technician, Pawn patient)
    {
        int waitTicks = SelfRepairSecondsPerCycle * TicksPerGameSecond;
        if (technician == patient)
        {
            Toil wait = Toils_General.Wait(waitTicks);
            wait.WithProgressBarToilDelay(TargetIndex.B);
            wait.FailOnDestroyedOrNull(TargetIndex.A);
            yield return wait;
        }
        else
        {
            yield return MakePausedContactWaitToil(
                technician,
                patient,
                waitTicks,
                "CanoptekRepairWaitScarabInjury");
        }

        Toil apply = ToilMaker.MakeToil("CanoptekRepairSelfScarabInjuryApply");
        apply.initAction = delegate
        {
            Hediff_Injury injury = FindNextInjuredScarabInjury(patient);
            if (injury == null)
                return;

            Need_Necrodermis need = technician.needs?.TryGetNeed<Need_Necrodermis>();
            if (need == null || need.CurLevel <= 1e-5f)
                return;

            float necroSpend = Mathf.Min(SelfScarabInjuryNecroPerRepairCycle, need.CurLevel);
            float healPoints = necroSpend * (SelfScarabInjuryHealHpPerGameSecond / SelfScarabInjuryNecroPerGameSecond);
            float healed = Mathf.Min(injury.Severity, healPoints);
            if (healed <= 1e-5f)
                return;

            injury.Severity = Mathf.Max(0f, injury.Severity - healed);
            if (injury.Severity <= 1e-4f)
                patient.health.RemoveHediff(injury);

            ConsumeNecrodermis(need, necroSpend);
        };
        yield return apply;
    }

    private static int GetStructureRepairDurationTicks(bool necronStructure)
    {
        int seconds = necronStructure ? NecronStructureRepairSeconds : OtherStructureRepairSeconds;
        return Mathf.Max(1, seconds * TicksPerGameSecond);
    }

    private static void ApplyMissingScarabPartRestore(Pawn technician, Pawn patient)
    {
        if (patient?.health?.hediffSet == null)
            return;

        Need_Necrodermis need = technician.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null || need.CurLevel < SelfRepairNecroPerPart - 1e-4f)
            return;

        Hediff_MissingPart missing = FindNextMissingScarabPart(patient);
        if (missing?.Part == null)
            return;

        ConsumeNecrodermis(need, SelfRepairNecroPerPart);
        patient.health.RestorePart(missing.Part);
    }

    private static void ApplyGeneralMissingPartRestoreAlly(Pawn technician, Pawn patient)
    {
        if (patient?.health?.hediffSet == null)
            return;

        Need_Necrodermis need = technician.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null || need.CurLevel < SelfRepairNecroPerPart - 1e-4f)
            return;

        Hediff_MissingPart mp = FindNextGeneralMissingPartForNecronAlly(patient);
        if (mp?.Part == null)
            return;

        ConsumeNecrodermis(need, SelfRepairNecroPerPart);
        patient.health.RestorePart(mp.Part);
    }

    /// <summary>Any non-solid optional missing anatomy (hands, tails, jaws); skips missing core vitals guarded by RimWorld).</summary>
    private static Hediff_MissingPart FindNextGeneralMissingPartForNecronAlly(Pawn patient)
    {
        if (patient?.health?.hediffSet?.hediffs == null)
            return null;

        foreach (Hediff h in patient.health.hediffSet.hediffs)
        {
            if (h is Hediff_MissingPart mp && mp.Part != null && mp.Part.def != null)
                return mp;
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="technician"/> has enough necrodermis right now for every repair step this job would run
    /// on <paramref name="patient"/> (injury pulse(s) then missing-part restore — same order as <see cref="MakeNewToils"/>).
    /// Call from job giver / target search so we do not start a job that cannot pay at apply time.
    /// </summary>
    internal static bool TechnicianMeetsNecroThresholdForPatient(Pawn technician, Pawn patient)
    {
        if (technician == null || patient == null)
            return false;
        Need_Necrodermis need = technician.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null || need.CurLevel <= 0.001f)
            return false;

        bool missing = false;
        bool injury = false;

        if (technician == patient)
        {
            missing = HasMissingScarabPart(patient);
            injury = HasInjuredScarabUnitParts(patient);
        }
        else if (HarmonyPatch_ScarabSwarmChassis.IsScarabSwarm(patient))
        {
            missing = HasMissingScarabPart(patient);
            injury = HasInjuredScarabUnitParts(patient);
        }
        else if (NechEnergyUtility.IsNecronPawn(patient))
        {
            missing = FindNextGeneralMissingPartForNecronAlly(patient) != null;
            injury = NecronStasisHealing.SumHealableInjurySeverity(patient) > 0.001f;
        }
        else if (patient.RaceProps.IsMechanoid)
        {
            missing = FindNextGeneralMissingPartForNecronAlly(patient) != null;
            injury = NecronStasisHealing.SumHealableInjurySeverity(patient) > 0.001f;
        }
        else
            return false;

        if (!missing && !injury)
            return false;

        float required = 0f;
        if (injury)
            required += SelfScarabInjuryNecroPerRepairCycle;
        if (missing)
            required += SelfRepairNecroPerPart;
        return need.CurLevel >= required - 1e-4f;
    }

    /// <summary>Used by job giver to pick reachable damaged allies.</summary>
    internal static bool NeedsFriendlyNecronAllyRepair(Pawn patient)
    {
        if (patient == null || patient.Dead || !patient.Spawned)
            return false;
        if (HarmonyPatch_ScarabSwarmChassis.IsScarabSwarm(patient))
            return HasMissingScarabPart(patient) || HasInjuredScarabUnitParts(patient);
        if (!NechEnergyUtility.IsNecronPawn(patient))
            return false;
        if (FindNextGeneralMissingPartForNecronAlly(patient) != null)
            return true;
        return NecronStasisHealing.SumHealableInjurySeverity(patient) > 0.01f;
    }

    /// <summary>Vanilla / non-dynasty mechanoids (not <see cref="NechEnergyUtility.IsNecronPawn"/>).</summary>
    internal static bool NeedsFriendlyVanillaMechRepair(Pawn patient)
    {
        if (patient == null || patient.Dead || !patient.Spawned)
            return false;
        if (!patient.RaceProps.IsMechanoid || NechEnergyUtility.IsNecronPawn(patient))
            return false;
        if (FindNextGeneralMissingPartForNecronAlly(patient) != null)
            return true;
        return NecronStasisHealing.SumHealableInjurySeverity(patient) > 0.01f;
    }

    internal static bool HasMissingScarabPart(Pawn pawn) =>
        FindNextMissingScarabPart(pawn) != null;

    internal static bool HasInjuredScarabUnitParts(Pawn pawn)
    {
        return FindNextInjuredScarabInjury(pawn) != null;
    }

    private static Hediff_Injury FindNextInjuredScarabInjury(Pawn pawn)
    {
        if (pawn?.health?.hediffSet?.hediffs == null)
            return null;

        for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
        {
            if (pawn.health.hediffSet.hediffs[i] is Hediff_Injury inj
                && !inj.IsPermanent()
                && inj.Severity > 0.001f
                && inj.Part?.def?.defName == "GW40K_ScarabUnit")
                return inj;
        }

        return null;
    }

    private static Hediff_MissingPart FindNextMissingScarabPart(Pawn pawn)
    {
        if (pawn?.health?.hediffSet?.hediffs == null)
            return null;

        for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
        {
            if (pawn.health.hediffSet.hediffs[i] is Hediff_MissingPart mp
                && mp.Part?.def?.defName == "GW40K_ScarabUnit")
                return mp;
        }

        return null;
    }

    private static void ApplyStructureRepair(Pawn pawn, Thing target, bool necronStructure)
    {
        if (pawn == null || target == null || target.Destroyed || !target.def.useHitPoints)
            return;
        if (target.HitPoints >= target.MaxHitPoints)
            return;

        Need_Necrodermis need = pawn.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null || need.CurLevel <= 0.001f)
            return;

        float ratio = necronStructure
            ? NecronStructureHpPercentPerNecroPercent
            : OtherStructureHpPercentPerNecroPercent;

        float missing = target.MaxHitPoints - target.HitPoints;
        float maxHealableByNecro = need.CurLevel * target.MaxHitPoints * ratio;
        float healAmount = Mathf.Min(missing, maxHealableByNecro);
        if (healAmount <= 0.001f)
            return;

        int healInt = Mathf.Clamp(Mathf.RoundToInt(healAmount), 1, Mathf.RoundToInt(missing));
        target.HitPoints = Mathf.Min(target.MaxHitPoints, target.HitPoints + healInt);

        float necroSpent = healInt / (target.MaxHitPoints * ratio);
        ConsumeNecrodermis(need, necroSpent);
    }

    /// <summary>GrimWorld / Undying-Legions building defs (prefix), never pawns.</summary>
    internal static bool IsNecronStructureBuilding(Thing t)
    {
        if (t is Pawn)
            return false;

        string defName = t?.def?.defName;
        if (string.IsNullOrEmpty(defName))
            return false;
        return defName.StartsWith("GW40K_") || defName.StartsWith("GW_UD_");
    }

    private static void ConsumeNecrodermis(Need_Necrodermis need, float amount)
    {
        if (need == null || amount <= 0f)
            return;
        need.CurLevel = Mathf.Max(0f, need.CurLevel - amount);
    }

    private static bool IsInTouchRange(Pawn technician, Pawn patient)
    {
        if (technician == null || patient == null || !technician.Spawned || !patient.Spawned)
            return false;
        if (technician.MapHeld != patient.MapHeld)
            return false;
        return technician.Position.AdjacentTo8WayOrInside(patient.Position);
    }

    /// <summary>
    /// Ally-only repair wait that pauses progress while out of touch range, then resumes after re-contact.
    /// This keeps a single repair job active instead of failing/restarting every relocation.
    /// </summary>
    private Toil MakePausedContactWaitToil(Pawn technician, Pawn patient, int totalTicks, string debugName)
    {
        int remainingTicks = Mathf.Max(1, totalTicks);
        Toil toil = ToilMaker.MakeToil(debugName);
        toil.defaultCompleteMode = ToilCompleteMode.Never;
        toil.FailOnDestroyedOrNull(TargetIndex.A);
        toil.tickAction = delegate
        {
            if (patient == null || technician == null || patient.Dead || technician.Dead)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            if (IsInTouchRange(technician, patient))
            {
                remainingTicks--;
                if (remainingTicks <= 0)
                    ReadyForNextToil();
                return;
            }

            if (!technician.pather.Moving)
                technician.pather.StartPath(patient, PathEndMode.Touch);
        };
        toil.WithProgressBar(TargetIndex.B, () => 1f - Mathf.Clamp01((float)remainingTicks / Mathf.Max(1, totalTicks)));
        return toil;
    }
}
