using System;
using System.Collections.Generic;
using HarmonyLib;
using NecronGeneUtil;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

// Consolidated Harmony for Canoptek constructs (scarabs), Spyder-adjacent combat (integrated verbs),
// and float-menu / attack-verb fixes that exist specifically to support that pipeline.

// ── Scarab swarm: chassis / death rules (helpers + AddHediff) ─────────────────

/// <summary>
/// Scarab swarm: root chassis must not take structural damage (low HP on core killed the pawn before units were destroyed).
/// </summary>
public static class HarmonyPatch_ScarabSwarmChassis
{
    public const string ScarabSwarmRaceDefName = "GW40K_ScarabSwarm";
    public const string ScarabChassisPartDefName = "GW40K_ScarabSwarm_Chassis";
    public const string ScarabUnitPartDefName = "GW40K_ScarabUnit";

    /// <summary>
    /// While set, <see cref="HarmonyPatch_ScarabSwarm_BlockChassisAddHediff"/> allows a single
    /// <see cref="Hediff_MissingPart"/> on the swarm chassis (normally blocked so stray damage cannot kill the core early).
    /// </summary>
    [ThreadStatic]
    internal static bool ForceScarabChassisMissing;

    public static bool IsScarabSwarm(Pawn pawn) =>
        pawn?.def?.defName == ScarabSwarmRaceDefName;

    public static bool AnyLivingScarabUnit(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null)
            return false;
        foreach (BodyPartRecord bp in pawn.health.hediffSet.GetNotMissingParts())
        {
            if (bp.def.defName == ScarabUnitPartDefName)
                return true;
        }

        return false;
    }

    public static int ScarabUnitSlotCount(Pawn pawn)
    {
        BodyDef body = pawn?.RaceProps?.body;
        if (body?.AllParts == null)
            return 0;
        int n = 0;
        foreach (BodyPartRecord r in body.AllParts)
        {
            if (r.def.defName == ScarabUnitPartDefName)
                n++;
        }

        return n;
    }

    /// <summary>
    /// When every <see cref="ScarabUnitPartDefName"/> is gone, remove the chassis part so health UI and death line up.
    /// </summary>
    public static void EnsureChassisMissingIfNoLivingUnits(Pawn pawn)
    {
        if (!IsScarabSwarm(pawn) || pawn.Dead || pawn.Destroyed || pawn.health?.hediffSet == null)
            return;
        if (AnyLivingScarabUnit(pawn))
            return;

        BodyPartRecord chassisPart = null;
        foreach (BodyPartRecord r in pawn.health.hediffSet.GetNotMissingParts())
        {
            if (r.def.defName == ScarabChassisPartDefName)
            {
                chassisPart = r;
                break;
            }
        }

        if (chassisPart == null)
            return;

        Hediff missing = HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, chassisPart);
        ForceScarabChassisMissing = true;
        try
        {
            pawn.health.AddHediff(missing);
        }
        finally
        {
            ForceScarabChassisMissing = false;
        }
    }

    internal static bool ShouldBlockHediffOnChassis(Pawn pawn, Hediff hediff, BodyPartRecord part)
    {
        if (!IsScarabSwarm(pawn) || hediff == null)
            return false;
        BodyPartRecord effective = part ?? hediff.Part;
        if (effective == null || effective.def.defName != ScarabChassisPartDefName)
            return false;
        if (ForceScarabChassisMissing && hediff is Hediff_MissingPart)
            return false;
        return hediff is Hediff_Injury || hediff is Hediff_MissingPart;
    }
}

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff), new Type[]
{
    typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult)
})]
public static class HarmonyPatch_ScarabSwarm_BlockChassisAddHediff
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn ___pawn, Hediff hediff, BodyPartRecord part, DamageInfo? dinfo, DamageWorker.DamageResult result)
    {
        if (HarmonyPatch_ScarabSwarmChassis.ShouldBlockHediffOnChassis(___pawn, hediff, part))
            return false;
        return true;
    }

    [HarmonyPostfix]
    public static void Postfix(Pawn ___pawn)
    {
        HarmonyPatch_ScarabSwarmChassis.EnsureChassisMissingIfNoLivingUnits(___pawn);
    }
}

// ── Scarab swarm: never downed ───────────────────────────────────────────────

/// <summary>
/// Canoptek scarab swarms fight until destroyed; they do not enter the downed state.
/// </summary>
[HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
[HarmonyPriority(Priority.First)]
public static class HarmonyPatch_ScarabNeverDowned
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn ___pawn)
    {
        if (___pawn?.def?.defName == HarmonyPatch_ScarabSwarmChassis.ScarabSwarmRaceDefName)
            return false;
        return true;
    }
}

// ── Scarab swarm: terrain pathing ─────────────────────────────────────────────

/// <summary>
/// Canoptek scarab swarms ignore terrain path cost penalties (base cardinal move cost only).
/// </summary>
[HarmonyPatch(typeof(Pawn_PathFollower), "CostToMoveIntoCell", new[] { typeof(IntVec3) })]
public static class HarmonyPatch_ScarabTerrain
{
    private static readonly AccessTools.FieldRef<Pawn_PathFollower, Pawn> PawnRef =
        AccessTools.FieldRefAccess<Pawn_PathFollower, Pawn>("pawn");

    [HarmonyPostfix]
    public static void Postfix(Pawn_PathFollower __instance, ref float __result)
    {
        Pawn pawn = PawnRef(__instance);
        if (pawn?.def?.defName != HarmonyPatch_ScarabSwarmChassis.ScarabSwarmRaceDefName)
            return;

        __result = pawn.TicksPerMoveCardinal;
    }
}

// ── Canoptek: digest interrupted by damage ───────────────────────────────────

/// <summary>
/// If a Canoptek construct is attacked while actively digesting, eject the held item and immediately retaliate.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
public static class HarmonyPatch_CanoptekConsumeThreatResponse
{
    public static void Postfix(Pawn __instance, DamageInfo dinfo)
    {
        if (__instance == null || __instance.Dead || __instance.Destroyed || !__instance.Spawned)
            return;
        if (!NecrodermisIngestionUtility.IsCanoptek(__instance))
            return;
        if (__instance.jobs?.curDriver is not JobDriver_CanoptekConsume)
            return;

        ThingComp_CanoptekConsumePolicy comp = __instance.TryGetComp<ThingComp_CanoptekConsumePolicy>();
        if (comp == null)
            return;

        Thing held = comp.GetCurrentConsumedThing();
        if (held == null || comp.IsEjectSuppressed())
            return;

        Thing aggressor = dinfo.Instigator;
        if (aggressor == null || aggressor == __instance || aggressor.Destroyed)
            return;
        if (aggressor.Map != __instance.Map)
            return;
        if (!GenHostility.HostileTo(aggressor, __instance))
            return;

        bool ejected = comp.TryEjectCurrentConsumedThing();
        if (!ejected)
            return;

        Verb preferredRanged = NechIntegratedAttackUtility.TryGetPreferredRangedVerb(__instance);
        bool ranged = preferredRanged != null
            || (__instance.equipment?.Primary != null && !__instance.equipment.Primary.def.IsMeleeWeapon);

        Job retaliate;
        if (ranged)
        {
            retaliate = JobMaker.MakeJob(JobDefOf.AttackStatic, aggressor);
            if (preferredRanged != null)
                retaliate.verbToUse = preferredRanged;
        }
        else
        {
            retaliate = JobMaker.MakeJob(JobDefOf.AttackMelee, aggressor);
        }

        retaliate.playerForced = true;
        __instance.jobs?.TryTakeOrderedJob(retaliate, JobTag.Misc, requestQueueing: false);
    }
}

// ── Canoptek: no friendly melee-threat retaliate job ─────────────────────────

/// <summary>
/// Vanilla <see cref="JobGiver_ReactToCloseMeleeThreat"/> issues <see cref="JobDefOf.AttackMelee"/> without faction check;
/// Canoptek only keeps that job when <see cref="GenHostility"/> considers the threat hostile.
/// </summary>
[HarmonyPatch(typeof(JobGiver_ReactToCloseMeleeThreat), "TryGiveJob")]
public static class HarmonyPatch_CanoptekMeleeThreatNoFriendlyRetaliate
{
    [HarmonyPostfix]
    public static void Postfix(Pawn pawn, ref Job __result)
    {
        if (!NechEnergyUtility.IsNecronPawn(pawn))
            return;
        if (__result == null || __result.def != JobDefOf.AttackMelee)
            return;

        Thing target = __result.targetA.Thing;
        if (target == null)
        {
            __result = null;
            return;
        }

        if (!GenHostility.HostileTo(target, pawn))
        {
            __result = null;
            if (pawn.mindState != null && pawn.mindState.meleeThreat == target)
                pawn.mindState.meleeThreat = null;
        }
    }
}

// ── Scarab swarm: minimum movement speed ─────────────────────────────────────

/// <summary>
/// Scarab swarms never fall below 25% movement capacity regardless of unit losses.
/// Without this floor, losing MovingLimbCore units degrades Moving toward zero,
/// stranding the swarm before all units are dead.
/// </summary>
[HarmonyPatch(typeof(PawnCapacitiesHandler), nameof(PawnCapacitiesHandler.GetLevel))]
public static class HarmonyPatch_ScarabMinMoveSpeed
{
    private const float MinMoving = 0.25f;

    [HarmonyPostfix]
    public static void Postfix(PawnCapacityDef capacity, Pawn ___pawn, ref float __result)
    {
        if (capacity != PawnCapacityDefOf.Moving) return;
        if (!HarmonyPatch_ScarabSwarmChassis.IsScarabSwarm(___pawn)) return;
        if (__result < MinMoving)
            __result = MinMoving;
    }
}

// ── Canoptek: necrodermis + eject gizmos ──────────────────────────────────────

[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
[HarmonyPriority(Priority.Last)]
[StaticConstructorOnStartup]
public static class HarmonyPatch_CanoptekNecrodermisGizmo
{
    private static readonly Texture2D SpyderAttackIcon =
        ContentFinder<Texture2D>.Get("UI/Abilities/GW40K_SpyderAttack", false);

    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
    {
        foreach (Gizmo g in GizmoEnumerationSafety.PassThroughWithSafety(gizmos, __instance, "CanoptekNecrodermis"))
            yield return g;

        if (__instance == null || __instance.Faction != Faction.OfPlayer)
            yield break;
        if (!ControlNodeUtility.IsCanoptek(__instance))
            yield break;

        Need_Necrodermis need = __instance.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null)
            yield break;

        ThingComp_CanoptekConsumePolicy consumeComp = __instance.TryGetComp<ThingComp_CanoptekConsumePolicy>();
        yield return new Gizmo_CanoptekNecrodermis(__instance, need);
        if (consumeComp != null)
            yield return new Gizmo_CanoptekEject(__instance, consumeComp);

        if (__instance.Drafted && HediffComp_ControlNodeTracker.GetControllerOfScarab(__instance) != null)
        {
            yield return MakeScarabRangedAttackCommand(__instance);
            yield return MakeScarabMeleeAttackCommand(__instance);
        }
    }

    private static Command_Action MakeScarabRangedAttackCommand(Pawn scarab)
    {
        // Prefer an integrated ranged verb (e.g. Spyder particle beamer controlled via Control Node).
        // Falls back to null for pure-melee Canoptek like scarab swarms.
        Verb integratedRanged = NechIntegratedAttackUtility.TryGetPreferredRangedVerb(scarab);
        float verbRange = integratedRanged?.verbProps?.range ?? 0f;

        return new Command_Action
        {
            defaultLabel = "Ranged attack",
            defaultDesc = "Order this controlled construct to perform a ranged attack.",
            icon = SpyderAttackIcon ?? TexCommand.Attack,
            hotKey = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Misc4"),
            action = () =>
            {
                if (scarab == null || scarab.Dead || !scarab.Spawned || scarab.Map == null)
                    return;
                Find.Targeter.BeginTargeting(new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetBuildings = true,
                    canTargetLocations = false,
                    validator = t =>
                    {
                        if (!t.IsValid || !t.HasThing) return false;
                        Thing thing = t.Thing;
                        if (thing == null || thing.Destroyed || thing.Map != scarab.Map) return false;
                        if (verbRange > 0f && scarab.Position.DistanceTo(thing.Position) > verbRange)
                            return false;
                        return GenHostility.HostileTo(thing, scarab);
                    }
                }, target =>
                {
                    if (!target.IsValid || !target.HasThing) return;
                    // Re-resolve verb at cast time in case availability changed.
                    Verb attackVerb = NechIntegratedAttackUtility.TryGetPreferredRangedVerb(scarab);
                    Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target.Thing);
                    if (attackVerb != null)
                        job.verbToUse = attackVerb;
                    job.playerForced = true;
                    scarab.jobs?.TryTakeOrderedJob(job, JobTag.Misc, requestQueueing: false);
                });
            }
        };
    }

    private static Command_Action MakeScarabMeleeAttackCommand(Pawn scarab)
    {
        return new Command_Action
        {
            defaultLabel = "Melee attack",
            defaultDesc = "Order this controlled scarab to perform a melee attack order.",
            icon = TexCommand.AttackMelee,
            hotKey = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Misc3"),
            action = () =>
            {
                if (scarab == null || scarab.Dead || !scarab.Spawned || scarab.Map == null)
                    return;
                Find.Targeter.BeginTargeting(new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetBuildings = true,
                    canTargetLocations = false,
                    validator = t =>
                    {
                        if (!t.IsValid || !t.HasThing) return false;
                        Thing thing = t.Thing;
                        return thing != null && !thing.Destroyed && thing.Map == scarab.Map && GenHostility.HostileTo(thing, scarab);
                    }
                }, target =>
                {
                    if (!target.IsValid || !target.HasThing) return;
                    Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target.Thing);
                    job.playerForced = true;
                    scarab.jobs?.TryTakeOrderedJob(job, JobTag.Misc, requestQueueing: false);
                });
            }
        };
    }
}

// ── Scarab: auto-switch to Repair when attacked in Produce mode ─────────────────

/// <summary>
/// If a scarab is in Produce mode with auto-mode enabled and takes damage, switch it to Repair.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
public static class HarmonyPatch_ScarabProduceAutoRepair
{
    public static void Postfix(Pawn __instance)
    {
        if (__instance == null || __instance.Dead || !__instance.Spawned) return;
        if (!HarmonyPatch_ScarabSwarmChassis.IsScarabSwarm(__instance)) return;

        GameComponent_CanoptekConstructModes modes = GameComponent_CanoptekConstructModes.Current;
        if (modes == null) return;
        if (modes.GetMode(__instance, ControlNodeMode.Consume) != ControlNodeMode.Produce) return;
        if (!modes.GetAutoMode(__instance)) return;

        modes.SetMode(__instance, ControlNodeMode.Repair);
    }
}

// ── Scarab: panic self-destruct on damage (enemy AI) ───────────────────────────

/// <summary>
/// Enemy scarab swarms: when below 25% HP, each damage event has a chance to attempt detonation.
/// </summary>
[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.PostApplyDamage))]
public static class HarmonyPatch_ScarabPanicSelfDestruct
{
    private const float HpThresholdPct = 0.25f;
    private const float PanicDetonateChance = 0.31f;

    [HarmonyPostfix]
    public static void Postfix(Pawn ___pawn)
    {
        Pawn pawn = ___pawn;
        if (!ScarabSelfDestructUtility.IsScarabSwarm(pawn))
            return;
        if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned)
            return;
        if (pawn.Faction == Faction.OfPlayer)
            return;
        if (pawn.health == null || pawn.health.summaryHealth == null)
            return;
        if (pawn.health.summaryHealth.SummaryHealthPercent >= HpThresholdPct)
            return;
        if (!Rand.Chance(PanicDetonateChance))
            return;

        AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail("GW40K_ScarabSelfDestruct");
        ScarabSelfDestructProperties props = null;
        if (def?.comps != null)
        {
            for (int i = 0; i < def.comps.Count; i++)
            {
                if (def.comps[i] is ScarabSelfDestructProperties s)
                {
                    props = s;
                    break;
                }
            }
        }

        if (props == null)
            return;

        ScarabSelfDestructUtility.TryDetonate(pawn, props);
    }
}

// ── Float menu: ranged attack row + NRE guard ─────────────────────────────────

/// <summary>
/// Vanilla <see cref="FloatMenuUtility.UseRangedAttack"/> can throw for some pawns when building squad attack UI.
/// Canoptek scarabs: no ranged. Necron mechs with integrated projectile (Spyder): true so float menu offers ranged.
/// </summary>
[HarmonyPatch(typeof(FloatMenuUtility), nameof(FloatMenuUtility.UseRangedAttack))]
public static class HarmonyPatch_FloatMenuUtility_UseRangedAttack
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn pawn, ref bool __result)
    {
        if (pawn == null)
        {
            __result = false;
            return false;
        }

        if (NechEnergyUtility.IsCanoptek(pawn))
        {
            __result = false;
            return false;
        }

        if (pawn.def?.GetModExtension<NecronMechExtension>() != null)
        {
            __result = NechIntegratedAttackUtility.TryGetPreferredRangedVerb(pawn) != null;
            return false;
        }

        return true;
    }

    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception, ref bool __result)
    {
        if (__exception is NullReferenceException)
        {
            __result = false;
            return null;
        }

        return __exception;
    }
}

// ── Integrated verbs: null equipment force-miss (Spyder beamer) ─────────────────

/// <summary>
/// ThingDef-linked verbs have no <see cref="Verb.EquipmentSource"/>; vanilla <see cref="Verb_LaunchProjectile.TryCastShot"/>
/// passes null into <see cref="VerbProperties.GetForceMissFactorFor"/> and can NRE.
/// </summary>
[HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.GetForceMissFactorFor))]
public static class HarmonyPatch_VerbProperties_GetForceMissFactorFor_NullEquipment
{
    [HarmonyPrefix]
    public static bool Prefix(Thing equipment, ref float __result)
    {
        if (equipment != null)
            return true;
        __result = 1f;
        return false;
    }
}

// ── Integrated / dual-mode ranged: TryGetAttackVerb + AttackStatic TryStartAttack ─

/// <summary>
/// Force <c>allowManualCastWeapons</c> when a non-melee projectile verb exists (Spyder beamer, staff shoot mode, etc.).
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.TryGetAttackVerb), typeof(Thing), typeof(bool), typeof(bool))]
public static class HarmonyPatch_NechTryGetAttackVerbAllowIntegrated
{
    [HarmonyPrefix]
    public static void Prefix(Pawn __instance, ref bool allowManualCastWeapons)
    {
        if (allowManualCastWeapons)
            return;
        // requireAvailable: true — don't enable manual-cast path for unavailable verbs.
        // Siege cannon is also excluded by TryGetPreferredRangedVerb regardless.
        if (NechIntegratedAttackUtility.TryGetPreferredRangedVerb(__instance, requireAvailable: true) == null)
            return;
        allowManualCastWeapons = true;
    }

    /// <summary>
    /// Switchable staffs (and integrated beamers) expose melee tools alongside shoot verbs; vanilla may pick a melee
    /// tool for a distant target, which grays out draft right-click attack as "out of range".
    /// </summary>
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance, Thing target, ref Verb __result)
    {
        if (__instance == null || target == null || !target.Spawned)
            return;

        float dist = __instance.Position.DistanceTo(target.Position);

        if (__result != null && !__result.IsMeleeAttack && __result.verbProps != null
            && dist <= __result.verbProps.range + 0.25f)
            return;

        if (__result != null && __result.IsMeleeAttack && __result.verbProps != null
            && dist <= __result.verbProps.range + 0.25f)
            return;

        // requireAvailable: true — must not return a verb whose Available() is false.
        // Without this, TryGetPreferredRangedVerb returns the siege cannon (range 500,
        // highest range) and TryStartCastOn fires it directly without re-checking Available().
        Verb ranged = NechIntegratedAttackUtility.TryGetPreferredRangedVerb(__instance, requireAvailable: true);
        if (ranged?.verbProps == null)
            return;

        if (dist <= ranged.verbProps.range + 0.25f)
            __result = ranged;
    }
}

/// <summary>
/// When vanilla <see cref="Pawn.TryStartAttack"/> fails on <see cref="JobDefOf.AttackStatic"/>, retry with job / integrated ranged verb.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.TryStartAttack))]
public static class HarmonyPatch_NechTryStartAttack_AttackStaticVerb
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance, LocalTargetInfo targ, ref bool __result)
    {
        if (__result)
            return;
        if (__instance == null)
            return;
        if (__instance.stances?.FullBodyBusy == true)
            return;
        if (__instance.WorkTagIsDisabled(WorkTags.Violent))
            return;

        Job job = __instance.CurJob;
        if (job == null || job.def != JobDefOf.AttackStatic)
            return;

        Verb v = job.verbToUse;
        if (v == null || v.IsMeleeAttack)
            v = NechIntegratedAttackUtility.TryGetPreferredRangedVerb(__instance, requireAvailable: false);

        if (v == null || v.IsMeleeAttack || v.verbProps == null)
            return;

        if (v is not Verb_LaunchProjectile)
            return;

        if (!v.Available())
            return;

        __result = v.TryStartCastOn(targ, false, true);
    }
}
