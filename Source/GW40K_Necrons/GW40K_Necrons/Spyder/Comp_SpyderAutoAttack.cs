using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace GW40K_Necrons;

public class CompProperties_SpyderAutoAttack : CompProperties
{
    public string commandLabel = "Particle Beamer";
    public string commandDesc  = "Fire the integrated particle beamer at a target. Right-click to toggle autonomous fire.";

    public CompProperties_SpyderAutoAttack() => compClass = typeof(Comp_SpyderAutoAttack);
}

/// <summary>
/// Tracks the auto-attack toggle for the Spyder's integrated particle beamer.
/// Drafted  → full verb-target gizmo with auto toggle (right-click).
/// Undrafted → disabled placeholder that shows range on hover and plays reject on click.
/// </summary>
public class Comp_SpyderAutoAttack : ThingComp
{
    public bool autoAttackEnabled = true;

    private int _ticksSinceLastScan;
    private const int ScanIntervalTicks = 60; // 1 game-second — fast enough to catch rushing pawns

    public CompProperties_SpyderAutoAttack Props => (CompProperties_SpyderAutoAttack)props;

    public override void PostExposeData()
    {
        Scribe_Values.Look(ref autoAttackEnabled, "spyderAutoAttack", true);
    }

    /// <summary>
    /// When auto-attack is on, scans every second for the nearest hostile in range
    /// and queues an AttackStatic job. CompTickRare (~4 s) was too slow to react to
    /// pawns rushing the Spyder; 1-second polling keeps attack latency minimal.
    /// </summary>
    public override void CompTick()
    {
        base.CompTick();
        if (++_ticksSinceLastScan < ScanIntervalTicks) return;
        _ticksSinceLastScan = 0;
        if (!autoAttackEnabled) return;
        if (parent is not Pawn pawn) return;
        if (pawn.Faction != Faction.OfPlayer) return;
        if (!pawn.Spawned || pawn.Dead || pawn.Map == null) return;
        // Never interrupt an in-progress stance (ability warmup, attack cast, etc.).
        // Without this, TryTakeOrderedJob cancels the siege mode activation warmup every tick.
        if (pawn.stances?.FullBodyBusy == true) return;
        // Auto-attack fires regardless of draft state — the auto toggle controls it explicitly.

        Verb verb = FindParticleBeamerVerb(pawn);
        float rangeSq    = (verb?.verbProps?.range ?? 23f);
        rangeSq         *= rangeSq;
        float minRange   = verb?.verbProps?.EffectiveMinRange(LocalTargetInfo.Invalid, pawn) ?? 5f;
        float minRangeSq = minRange * minRange;

        // ── Stale-job check (before Available() so it always runs) ────────────
        // Cancel any AttackStatic job whose target has left the valid range window.
        // Done first so the warmup line never persists across the map.
        if (pawn.jobs?.curJob?.def == JobDefOf.AttackStatic)
        {
            Pawn curTarget = pawn.jobs.curJob.targetA.Thing as Pawn;
            bool curValid  = curTarget != null
                          && !curTarget.Dead && !curTarget.Downed
                          && GenHostility.HostileTo(pawn, curTarget);
            if (curValid)
            {
                float curDistSq = (curTarget.Position - pawn.Position).LengthHorizontalSquared;
                curValid = curDistSq <= rangeSq && curDistSq >= minRangeSq;
            }
            if (curValid) return; // existing job is fine — let it run
            // Target invalid, out of range, or too close: cancel now regardless of Available().
            pawn.jobs.EndCurrentJob(JobCondition.InterruptOptional);
        }

        // Auto-attack requires the verb to be ready.
        if (verb == null || !verb.Available()) return;

        // Find nearest hostile pawn within the valid range window that the verb can hit.
        Pawn target = null;
        float bestDist = float.MaxValue;
        foreach (Pawn other in pawn.Map.mapPawns.AllPawnsSpawned)
        {
            if (other.Dead || other.Downed) continue;
            if (!GenHostility.HostileTo(pawn, other)) continue;
            float d = (other.Position - pawn.Position).LengthHorizontalSquared;
            if (d >= bestDist)  continue;
            if (d > rangeSq)    continue; // beyond max range
            if (d < minRangeSq) continue; // too close — explosion would hit the Spyder
            if (!verb.CanHitTarget(other)) continue; // range + LOS
            bestDist = d;
            target   = other;
        }

        if (target == null) return; // nothing in range — stale job already cancelled above

        Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
        // endIfCantShootTargetFromCurPos removed: it caused the job to terminate the moment
        // the target ducked behind cover, and the 1-second scan interval re-issues it quickly
        // enough that removing the flag is preferable to the flicker.
        pawn.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (parent is not Pawn pawn) yield break;
        if (pawn.Faction == null || !pawn.Faction.IsPlayer) yield break;

        // Hide the close-range attack gizmo while siege mode is active — the long-range
        // particle cannon (from the siege-mode hediff) is the active weapon during that time.
        if (HediffComp_SpyderSiegeMode.IsSiegeMode(pawn)) yield break;

        Verb verb = FindParticleBeamerVerb(pawn);
        if (verb == null) yield break;

        if (pawn.Drafted)
            yield return new Command_SpyderAutoAttack(verb, this);
        else
            yield return new Command_SpyderUndraftedAttack(verb, Props);
    }

    private static Verb FindParticleBeamerVerb(Pawn pawn) =>
        pawn.verbTracker?.AllVerbs?.Find(v => v is Verb_SpyderParticleBeamer);
}

/// <summary>
/// Drafted gizmo: fires the particle beamer at a chosen target.
/// Left-click → open targeter (always allowed — does NOT go through verb.Available()).
/// Right-click → toggle autonomous fire on/off.
///
/// Extends <see cref="Command_Action"/> instead of <see cref="Command_VerbTarget"/> so that
/// <see cref="Verb_SpyderParticleBeamer.Available"/> returning false when auto is OFF never
/// disables the button. <see cref="Command_VerbTarget"/> calls Available() during rendering
/// and sets <c>disabled = true</c> before any click can be processed; Command_Action has no
/// knowledge of verbs and therefore never interferes.
///
/// The hover radius ring is tracked via static fields and drawn by
/// <see cref="HarmonyPatch_BandwidthRingDraw"/> in the selection-overlay pre-render phase.
/// </summary>
[StaticConstructorOnStartup]
public class Command_SpyderAutoAttack : Command_Action
{
    private static readonly Texture2D TexOn;
    private static readonly Texture2D TexOff;

    static Command_SpyderAutoAttack()
    {
        TexOn  = ContentFinder<Texture2D>.Get("UI/Abilities/GW40K__BeamerAutoOn");
        TexOff = ContentFinder<Texture2D>.Get("UI/Abilities/GW40K__BeamerAutoOff");
    }

    /// <summary>Set each GUI frame when hovered; read by <see cref="HarmonyPatch_BandwidthRingDraw"/>.</summary>
    internal static Pawn  HoveredPawn;
    internal static float HoveredRange;

    private readonly Verb _verb;
    private readonly Comp_SpyderAutoAttack _comp;

    public Command_SpyderAutoAttack(Verb verb, Comp_SpyderAutoAttack comp)
    {
        _verb        = verb;
        _comp        = comp;
        defaultLabel = comp.Props.commandLabel;
        defaultDesc  = comp.Props.commandDesc;
        action       = OpenTargeter; // left-click handler via Command_Action.ProcessInput
    }

    /// <summary>
    /// Opens the targeter using explicit TargetingParameters + callback so that
    /// <see cref="Verb_SpyderParticleBeamer.Available"/> is never consulted.
    ///
    /// Mirrors Auto-ON manual targeting exactly:
    ///   • Sets <see cref="HarmonyPatch_NechGizmos.Command_SpyderRangedAttack"/> statics so
    ///     <see cref="HarmonyPatch_BandwidthRingDraw"/> draws the range ring and blast-AoE circle.
    ///   • <c>canTargetLocations = true</c> — matches the verb's own targetParams.
    ///   • Handles both Thing and cell (location) targets.
    ///   • Sets <c>job.verbToUse</c> to guarantee the particle beamer fires.
    /// </summary>
    private void OpenTargeter()
    {
        Pawn pawn = _verb?.CasterPawn;
        if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Map == null) return;
        if (HediffComp_SpyderSiegeMode.IsSiegeMode(pawn)) return;

        float range    = _verb?.verbProps?.range ?? 23f;
        float minRange = _verb?.verbProps?.EffectiveMinRange(LocalTargetInfo.Invalid, pawn) ?? 5f;
        float blast    = _verb?.verbProps?.defaultProjectile?.projectile?.explosionRadius ?? 0f;

        // Register with the ring-draw patch so the range ring (white) and blast-AoE circle
        // (orange, follows the cursor) appear during targeting — same as the fallback gizmo.
        // HarmonyPatch_BandwidthRingDraw.Postfix clears ActiveTargetingPawn the frame
        // after Find.Targeter.IsTargeting goes false.
        HarmonyPatch_NechGizmos.Command_SpyderRangedAttack.ActiveTargetingPawn  = pawn;
        HarmonyPatch_NechGizmos.Command_SpyderRangedAttack.ActiveTargetingRange = range;
        HarmonyPatch_NechGizmos.Command_SpyderRangedAttack.ActiveTargetingBlast = blast;

        Find.Targeter.BeginTargeting(
            new TargetingParameters
            {
                canTargetPawns     = true,
                canTargetBuildings = true,
                canTargetLocations = true,   // verb targetParams.canTargetLocations = true
                mapObjectTargetsMustBeAutoAttackable = false,
                validator = t =>
                {
                    if (!t.IsValid) return false;
                    if (t.HasThing && (t.Thing == null || t.Thing.Destroyed)) return false;
                    if (t.HasThing && t.Thing.Map != pawn.Map) return false;
                    IntVec3 pos  = t.HasThing ? t.Thing.Position : t.Cell;
                    float   dist = pawn.Position.DistanceTo(pos);
                    return dist >= minRange - 0.5f && dist <= range + 0.5f;
                }
            },
            target =>
            {
                if (!target.IsValid) return;
                // Support both Thing targets (pawn / building) and map-location targets.
                Job job = target.HasThing
                    ? JobMaker.MakeJob(JobDefOf.AttackStatic, target.Thing)
                    : JobMaker.MakeJob(JobDefOf.AttackStatic, target.Cell);
                job.verbToUse    = _verb;   // guarantee the beamer fires, not a melee tool
                job.playerForced = true;
                pawn.jobs?.TryTakeOrderedJob(job, JobTag.Misc, requestQueueing: false);
            });
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        icon = _comp.autoAttackEnabled ? TexOn : TexOff;

        float width = Mathf.Min(GetWidth(maxWidth), maxWidth);
        Rect rect = new Rect(topLeft.x, topLeft.y, width, 75f);

        // Track hover state for the radius ring drawn by HarmonyPatch_BandwidthRingDraw.
        Pawn pawn = _verb?.CasterPawn;
        if (pawn?.Spawned == true && Mouse.IsOver(rect))
        {
            HoveredPawn  = pawn;
            HoveredRange = _verb?.verbProps?.range ?? 0f;
        }
        else
        {
            HoveredPawn = null;
        }

        // Right-click: toggle auto-attack.
        if (Event.current.type == EventType.MouseDown
            && Event.current.button == 1
            && rect.Contains(Event.current.mousePosition))
        {
            _comp.autoAttackEnabled = !_comp.autoAttackEnabled;
            SoundDefOf.Click.PlayOneShotOnCamera();
            Event.current.Use();
            return new GizmoResult(GizmoState.Clear);
        }

        // Left-click is handled by action = OpenTargeter via Command_Action.ProcessInput.
        return base.GizmoOnGUI(topLeft, maxWidth, parms);
    }
}

/// <summary>
/// Undrafted placeholder: disabled verb-target button using the static beamer icon.
/// Shows the attack range ring on hover. Click plays the reject sound and posts a message.
/// </summary>
[StaticConstructorOnStartup]
public class Command_SpyderUndraftedAttack : Command_VerbTarget
{
    private static readonly Texture2D Tex;

    static Command_SpyderUndraftedAttack()
    {
        Tex = ContentFinder<Texture2D>.Get("UI/Abilities/GW40K_SpyderAttack");
    }

    public Command_SpyderUndraftedAttack(Verb verb, CompProperties_SpyderAutoAttack props)
    {
        this.verb      = verb;
        icon           = Tex;
        defaultLabel   = props.commandLabel;
        defaultDesc    = props.commandDesc;
        Disabled       = true;
        disabledReason = "AbilityDisabledUndrafted".Translate();
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        float width = Mathf.Min(GetWidth(maxWidth), maxWidth);
        Rect rect = new Rect(topLeft.x, topLeft.y, width, 75f);

        // Draw range ring on hover (base may skip this when Disabled).
        if (Mouse.IsOver(rect) && verb?.CasterPawn != null && verb.verbProps.range > 0f)
            GenDraw.DrawRadiusRing(verb.CasterPawn.Position, verb.verbProps.range);

        // Left-click: reject sound + message instead of opening targeter.
        if (Event.current.type == EventType.MouseDown
            && Event.current.button == 0
            && rect.Contains(Event.current.mousePosition))
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            Messages.Message("GW40K_SpyderMustBeDrafted".Translate(), MessageTypeDefOf.RejectInput, false);
            Event.current.Use();
            return new GizmoResult(GizmoState.Clear);
        }

        return base.GizmoOnGUI(topLeft, maxWidth, parms);
    }
}
