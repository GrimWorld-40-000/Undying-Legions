using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// For Necron construct pawns (NecronMechExtension):
///   - Strips all vanilla mechanitor/overseer/mech gizmos that come from CompOverseerSubject,
///     CompMechRepairable, and Pawn_MechanitorTracker.
///   - Injects Nechinator equivalents.
/// </summary>
/// <summary>Run after other <see cref="Pawn.GetGizmos"/> postfixes so we can strip draft toggles they pass through.</summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
[HarmonyPriority(Priority.Last)]
public static class HarmonyPatch_NechGizmos
{
    // Vanilla gizmo labels to suppress (case-insensitive substring match)
    private static readonly string[] VanillaStripLabels =
    {
        "mechanitor",
        "mech band",
        "make feral",
        "assign to overseer",
        "select overseer",
        "auto-repair",
        "mech energy",
        "overseer subject",
    };

    [HarmonyPostfix]
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
    {
        if (!NechUtility.IsNechControlled(__instance))
        {
            foreach (var g in GizmoEnumerationSafety.PassThroughWithSafety(gizmos, __instance, "NechPassthrough"))
                yield return g;
            yield break;
        }

        // Enemy Necrons: strip vanilla mech/mechanitor gizmos (same as we do for player
        // Necrons) but do NOT inject any Nechinator UI. This prevents CompOverseerSubject
        // and similar vanilla comps on the enemy Spyder from emitting "needs a mechanitor"
        // style messages that confuse the player's own Nech command UI.
        if (__instance.Faction != Faction.OfPlayer)
        {
            foreach (var g in GizmoEnumerationSafety.PassThroughWithSafety(gizmos, __instance, "NechEnemyPassthrough"))
            {
                if (g is Command cmd)
                {
                    string lbl = cmd.defaultLabel?.ToLowerInvariant() ?? string.Empty;
                    if (VanillaStripLabels.Any(s => lbl.Contains(s))) continue;
                }
                yield return g;
            }
            yield break;
        }

        bool hasCommander = HediffComp_NecronCommandTracker.GetCommanderOf(__instance) != null;
        bool hasControlNode = HediffComp_ControlNodeTracker.GetTracker(__instance) != null;
        bool isCommandable = hasCommander || hasControlNode;
        bool isSpyder = ControlNodeUtility.IsSpyder(__instance);
        // Spyders should only expose draft when they are actively commanded.
        bool canShowDraft = isSpyder ? hasCommander : isCommandable;
        bool sawAttackVerbGizmo = false;

        // Strip vanilla mech/overseer gizmos
        foreach (var g in GizmoEnumerationSafety.PassThroughWithSafety(gizmos, __instance, "NechStrip"))
        {
            // Always strip upstream draft toggles; we inject exactly one Nech draft when commanded (none when uncontrolled).
            // Vanilla may still emit a colonist-style draft (disabled / wrong tooltip) if icon or class name differs by version.
            if (IsUpstreamDraftToggleGizmo(g))
                continue;

            // Strip by type — CompOverseerSubject and CompMechRepairable gizmos
            if (g.GetType().FullName?.Contains("OverseerSubject") == true) continue;
            if (g.GetType().FullName?.Contains("MechRepair") == true) continue;

            // Strip by label
            string label = (g as Command)?.defaultLabel ?? string.Empty;
            bool strip = false;
            foreach (var banned in VanillaStripLabels)
            {
                if (label.IndexOf(banned, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    strip = true;
                    break;
                }
            }
            if (strip) continue;

            // Vanilla disables verb-targeting gizmos (ranged attack etc.) via IsPlayerControlled.
            // Nechs don't use the vanilla mech system, so fix up the disabled state here when commanded.
            // Command_SpyderAutoAttack extends Command_Action (not Command_VerbTarget) so it won't
            // match the cvt check below — mark it explicitly so the generic fallback isn't injected.
            if (g is Command_SpyderAutoAttack)
                sawAttackVerbGizmo = true;

            if (isCommandable && g is Command_VerbTarget cvt)
            {
                sawAttackVerbGizmo = true;
                var tr = Traverse.Create(cvt);
                if (tr.Field("disabled").GetValue<bool>())
                {
                    if (__instance.Drafted)
                    {
                        // Commanded + drafted: fully re-enable. Range gating is in HarmonyPatch_NechOrderedJobRange.
                        tr.Field("disabled").SetValue(false);
                        tr.Field("disabledReason").SetValue((string)null);
                    }
                    else
                    {
                        // Commanded but undrafted: keep disabled, replace confusing vanilla reason.
                        // Field is string; Translate() returns TaggedString (reflection SetValue won't coerce).
                        tr.Field("disabledReason").SetValue("GW40K_NechMustDraftToAttack".Translate().ToString());
                    }
                }
            }

            // Scarabs (and similar) bound to someone else's Control Node implant: no protocol commander
            // and no local tracker — isCommandable is false above — but Biotech treats them like colony mechs,
            // so Ability gizmos (leap / detonate) stay disabled ("uncontrolled").
            bool controlNodeLinked = HediffComp_ControlNodeTracker.GetControllerOfConstruct(__instance) != null;
            if (controlNodeLinked && g is Command_Ability cmdAbility)
            {
                var atr = Traverse.Create(cmdAbility);
                if (atr.Field("disabled").GetValue<bool>())
                {
                    atr.Field("disabled").SetValue(false);
                    atr.Field("disabledReason").SetValue((string)null);
                }
            }

            yield return g;
        }

        // ── Always-visible gizmos ────────────────────────────────────────────

        // Select commander — Command_SelectCommander draws the command-range ring on mouseover.
        yield return new Command_SelectCommander(__instance)
        {
            defaultLabel = "Select commander",
            defaultDesc  = "Select this construct's commanding nechinator.",
            icon         = TexCommand.SelectCarriedPawn,
            action       = () =>
            {
                Pawn overseer = HediffComp_NecronCommandTracker.GetCommanderOf(__instance);
                if (overseer != null)
                {
                    CameraJumper.TryJumpAndSelect(overseer);
                }
                else
                {
                    Messages.Message($"{__instance.LabelCap} has no commander.", MessageTypeDefOf.RejectInput, false);
                }
            }
        };

        // Explicit draft toggle — only when a Nechinator command link exists and pawn is not in a mental break.
        if (canShowDraft && __instance.drafter != null && __instance.Faction == Faction.OfPlayer
            && !__instance.InMentalState)
        {
            Command_Toggle toggleDraft = new Command_Toggle();
            toggleDraft.defaultLabel = "CommandDraftLabel".Translate();
            toggleDraft.defaultDesc = "CommandDraftDesc".Translate();
            toggleDraft.icon = TexCommand.Draft;
            toggleDraft.turnOnSound = SoundDefOf.DraftOn;
            toggleDraft.turnOffSound = SoundDefOf.DraftOff;
            toggleDraft.isActive = () => __instance.Drafted;
            toggleDraft.hotKey = NechCommandHotkeys.DraftToggle();
            toggleDraft.toggleAction = delegate
            {
                if (__instance.InMentalState) return;
                bool drafted = !__instance.Drafted;
                __instance.drafter.Drafted = drafted;
                if (!drafted)
                    __instance.jobs?.EndCurrentJob(JobCondition.InterruptForced, false);
            };
            yield return toggleDraft;
        }

        // Spyder fallback: some builds/mod stacks fail to surface integrated verb gizmos.
        // If no attack verb gizmo made it through and the unit is drafted, inject explicit orders.
        if (__instance.Drafted && ControlNodeUtility.IsSpyder(__instance) && !sawAttackVerbGizmo)
        {
            yield return MakeFallbackRangedAttackCommand(__instance);
            yield return MakeFallbackMeleeAttackCommand(__instance);
        }

        if (NechEnergyUtility.GetCapacitorComp(__instance) != null && GaussWeaponUtil.HasEquippedGaussWeapon(__instance)
            && __instance.Faction == Faction.OfPlayer)
            yield return new Gizmo_NechEnergy(__instance) { readOnly = !isCommandable };

        // Recharge-from-core gizmo disabled for now (core ↔ gauss transfer UX TBD).

        // ── Command Group gizmos ─────────────────────────────────────────────
        foreach (Gizmo cg in NecronCommandGroupGizmos.GetGizmos(__instance))
            yield return cg;

        if (!DebugSettings.ShowDevGizmos) yield break;

        // ── Dev gizmos ───────────────────────────────────────────────────────

        Command_Action devAssign = new Command_Action
        {
            defaultLabel = "DEV: Assign to commander",
            defaultDesc  = "Bind this construct to a commander (nechinator) on the map.",
            action       = () =>
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (Pawn pawn in __instance.Map?.mapPawns?.AllPawnsSpawned ?? Enumerable.Empty<Pawn>())
                {
                    var tracker = HediffComp_NecronCommandTracker.GetTracker(pawn);
                    if (tracker == null) continue;
                    string label2 = tracker.HasBandwidthFor(__instance)
                        ? pawn.LabelCap
                        : $"{pawn.LabelCap} (bandwidth full)";
                    options.Add(new FloatMenuOption(label2, () =>
                    {
                        if (pawn.Faction != null && __instance.Faction != pawn.Faction)
                            __instance.SetFaction(pawn.Faction);

                        // Unbind from any existing commander first
                        Pawn old = HediffComp_NecronCommandTracker.GetCommanderOf(__instance);
                        HediffComp_NecronCommandTracker.GetTracker(old)?.UnbindMech(__instance);

                        tracker.BindMech(__instance);

                        int used = (int)tracker.BandwidthUsed;
                        int max = (int)tracker.BandwidthMax;
                        Messages.Message(
                            $"{__instance.LabelCap} assigned to {pawn.LabelCap}. Bandwidth: {used}/{max}.",
                            MessageTypeDefOf.TaskCompletion,
                            false);
                    }));
                }
                if (options.Count == 0)
                    options.Add(new FloatMenuOption("No commanders on map", null));
                Find.WindowStack.Add(new FloatMenu(options));
            }
        };

        Command_Action devRemove = new Command_Action
        {
            defaultLabel = "DEV: Remove from commander",
            defaultDesc  = "Unbind this construct from its current commander.",
            action       = () =>
            {
                Pawn overseer = HediffComp_NecronCommandTracker.GetCommanderOf(__instance);
                if (overseer != null)
                {
                    HediffComp_NecronCommandTracker.GetTracker(overseer)?.UnbindMech(__instance);
                    Messages.Message($"{__instance.LabelCap} unbound from {overseer.LabelCap}.", MessageTypeDefOf.NeutralEvent, false);
                }
                else
                {
                    Messages.Message($"{__instance.LabelCap} has no commander.", MessageTypeDefOf.RejectInput, false);
                }
            }
        };

        // Must be standalone Command_Action gizmos — GizmoGridDrawer calls ProcessInput on the outer Gizmo only.
        // Gizmo_NecronDevTwin wrapped two Commands but never forwarded ProcessInput, so actions never ran.
        if (!hasCommander)
            yield return devAssign;
        else
            yield return devRemove;

        if (DebugSettings.godMode)
        {
            yield return new Command_Action
            {
                defaultLabel = "DEV: Force Rogue",
                defaultDesc = "Immediately force this construct into rogue behavior.",
                action = () =>
                {
                    CompNechUncontrolledTimer timer = __instance.TryGetComp<CompNechUncontrolledTimer>();
                    timer?.ForceRogue(__instance);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: Force Hostile",
                defaultDesc = "Immediately make this construct hostile (Necron faction or rogue fallback).",
                action = () =>
                {
                    CompNechUncontrolledTimer timer = __instance.TryGetComp<CompNechUncontrolledTimer>();
                    timer?.ForceHostile(__instance);
                }
            };
        }

    }

    /// <summary>True for vanilla (or mod) draft UI coming from the enumerator — never pass through for Nech-pipeline pawns.</summary>
    private static bool IsUpstreamDraftToggleGizmo(Gizmo g)
    {
        if (g is not Command_Toggle ct)
            return false;
        if (ct.icon == TexCommand.Draft)
            return true;
        string typeName = ct.GetType().Name ?? string.Empty;
        if (typeName.IndexOf("Draft", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        string fullName = ct.GetType().FullName ?? string.Empty;
        if (fullName.IndexOf("Draft", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        string label = (ct.defaultLabel ?? string.Empty).ToString();
        string draftLabel = "CommandDraftLabel".Translate().Resolve();
        if (label.Length > 0 && draftLabel.Length > 0 && string.Equals(label, draftLabel, StringComparison.Ordinal))
            return true;

        string desc = (ct.defaultDesc ?? string.Empty).ToString();
        string draftDesc = "CommandDraftDesc".Translate().Resolve();
        if (desc.Length > 8 && draftDesc.Length > 8 && string.Equals(desc, draftDesc, StringComparison.Ordinal))
            return true;

        KeyBindingDef draftKey = NechCommandHotkeys.DraftToggle();
        if (draftKey != null && ct.hotKey == draftKey)
            return true;

        return false;
    }

    private static Command_Action MakeFallbackRangedAttackCommand(Pawn pawn)
    {
        Verb beamer = NechIntegratedAttackUtility.TryGetPreferredRangedVerb(pawn);
        float verbRange = beamer?.verbProps?.range ?? 0f;
        float blastRadius = beamer?.verbProps?.defaultProjectile?.projectile?.explosionRadius ?? 0f;

        string desc = "Order this unit to perform a ranged attack.";
        if (verbRange > 0f || blastRadius > 0f)
        {
            desc += "\n\n";
            if (verbRange > 0f)
                desc += $"Range: {verbRange:0} tiles";
            if (verbRange > 0f && blastRadius > 0f)
                desc += "\n";
            if (blastRadius > 0f)
                desc += $"Blast radius: {blastRadius:0} tiles";
        }

        return new Command_SpyderRangedAttack(pawn, verbRange, blastRadius)
        {
            defaultLabel = "Ranged attack",
            defaultDesc = desc,
            icon = GetSpyderRangedAttackIcon(),
            hotKey = NecronDefOfs.Misc1,
            action = () =>
            {
                if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
                    return;

                Command_SpyderRangedAttack.ActiveTargetingPawn  = pawn;
                Command_SpyderRangedAttack.ActiveTargetingRange = verbRange;
                Command_SpyderRangedAttack.ActiveTargetingBlast = blastRadius;

                Find.Targeter.BeginTargeting(new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetBuildings = true,
                    canTargetLocations = false,
                    validator = t =>
                    {
                        if (!t.IsValid || !t.HasThing || t.Thing == null || t.Thing.Destroyed)
                            return false;
                        if (t.Thing.Map != pawn.Map)
                            return false;
                        if (verbRange > 0f && pawn.Position.DistanceTo(t.Thing.Position) > verbRange)
                            return false;
                        if (GenHostility.HostileTo(t.Thing, pawn))
                            return true;
                        if (t.Thing is Building b && b.Faction != null && b.Faction != pawn.Faction)
                            return true;
                        return false;
                    }
                }, target =>
                {
                    if (!target.IsValid || !target.HasThing) return;
                    Verb attackVerb = NechIntegratedAttackUtility.TryGetPreferredRangedVerb(pawn)
                        ?? pawn.TryGetAttackVerb(target.Thing);
                    if (attackVerb == null || attackVerb.IsMeleeAttack)
                    {
                        Messages.Message(
                            $"{pawn.LabelCap} has no usable ranged attack verb for that target.",
                            MessageTypeDefOf.RejectInput,
                            false);
                        return;
                    }
                    Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target.Thing);
                    job.verbToUse = attackVerb;
                    job.playerForced = true;
                    bool accepted = pawn.jobs?.TryTakeOrderedJob(job, JobTag.Misc, requestQueueing: false) == true;
                    if (!accepted)
                    {
                        Messages.Message(
                            $"{pawn.LabelCap} could not begin ranged attack.",
                            MessageTypeDefOf.RejectInput,
                            false);
                    }
                });
            }
        };
    }

    private static Texture2D GetSpyderRangedAttackIcon() =>
        NechGizmoAssetBootstrap.SpyderRangedAttackFallbackIcon ?? TexCommand.Attack;

    internal sealed class Command_SpyderRangedAttack : Command_Action
    {
        internal static Pawn HoveredPawn;
        internal static float HoveredRange;
        internal static float HoveredBlast;

        internal static Pawn ActiveTargetingPawn;
        internal static float ActiveTargetingRange;
        internal static float ActiveTargetingBlast;

        private readonly Pawn _pawn;
        private readonly float _range;
        private readonly float _blastRadius;

        internal Command_SpyderRangedAttack(Pawn pawn, float range, float blastRadius)
        {
            _pawn = pawn;
            _range = range;
            _blastRadius = blastRadius;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            if (_pawn?.Spawned == true && Mouse.IsOver(rect))
            {
                HoveredPawn = _pawn;
                HoveredRange = _range;
                HoveredBlast = _blastRadius;
            }
            else
            {
                HoveredPawn = null;
            }
            return base.GizmoOnGUI(topLeft, maxWidth, parms);
        }
    }

    /// <summary>
    /// "Select commander" button variant that draws the commander's Command Protocol
    /// radius ring while the mouse hovers over it — identical to the ring shown when
    /// hovering over the bandwidth gizmo itself.
    /// </summary>
    internal sealed class Command_SelectCommander : Command_Action
    {
        /// <summary>Set each GUI frame when hovered; read by <see cref="HarmonyPatch_BandwidthRingDraw"/>.</summary>
        internal static HediffComp_NecronCommandTracker HoveredTracker;

        private readonly Pawn _nech;

        internal Command_SelectCommander(Pawn nech)
        {
            _nech = nech;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            if (_nech?.Spawned == true && Mouse.IsOver(rect))
            {
                Pawn commander = HediffComp_NecronCommandTracker.GetCommanderOf(_nech);
                HoveredTracker = commander != null
                    ? HediffComp_NecronCommandTracker.GetTracker(commander)
                    : null;
            }
            else
            {
                HoveredTracker = null;
            }
            return base.GizmoOnGUI(topLeft, maxWidth, parms);
        }
    }

    private static Command_Action MakeFallbackMeleeAttackCommand(Pawn pawn)
    {
        return new Command_Action
        {
            defaultLabel = "Melee attack",
            defaultDesc = "Order this unit to perform a melee attack.",
            icon = TexCommand.AttackMelee,
            hotKey = NecronDefOfs.Misc2,
            action = () =>
            {
                if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
                    return;
                Find.Targeter.BeginTargeting(new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetBuildings = true,
                    canTargetLocations = false,
                    validator = t => t.IsValid && t.HasThing && t.Thing != null && !t.Thing.Destroyed
                        && t.Thing.Map == pawn.Map && GenHostility.HostileTo(t.Thing, pawn)
                }, target =>
                {
                    if (!target.IsValid || !target.HasThing) return;
                    Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target.Thing);
                    job.playerForced = true;
                    pawn.jobs?.TryTakeOrderedJob(job, JobTag.Misc, requestQueueing: false);
                });
            }
        };
    }
}
