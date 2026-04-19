using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
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
        if (__instance.def.GetModExtension<NecronMechExtension>() == null)
        {
            foreach (var g in GizmoEnumerationSafety.PassThroughWithSafety(gizmos, __instance, "NechPassthrough"))
                yield return g;
            yield break;
        }

        bool hasCommander = HediffComp_NecronCommandTracker.GetCommanderOf(__instance) != null;

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
            if (hasCommander && g is Command_VerbTarget cvt)
            {
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

            yield return g;
        }

        // ── Always-visible gizmos ────────────────────────────────────────────

        // Select commander
        yield return new Command_Action
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
        if (hasCommander && __instance.drafter != null && __instance.Faction == Faction.OfPlayer
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

        if (NechEnergyUtility.GetCapacitorComp(__instance) != null)
            yield return new Gizmo_NechEnergy(__instance);

        // Recharge-from-core gizmo disabled for now (core ↔ gauss transfer UX TBD).

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
}
