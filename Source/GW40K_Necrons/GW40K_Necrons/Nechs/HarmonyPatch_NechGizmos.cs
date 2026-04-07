using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// For Necron construct pawns (NecronMechExtension):
///   - Strips all vanilla mechanitor/overseer/mech gizmos that come from CompOverseerSubject,
///     CompMechRepairable, and Pawn_MechanitorTracker.
///   - Injects Nechinator equivalents.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
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
            foreach (var g in gizmos) yield return g;
            yield break;
        }

        // Strip vanilla mech/overseer gizmos
        foreach (var g in gizmos)
        {
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
                Pawn overseer = __instance.GetOverseer();
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

        if (!DebugSettings.ShowDevGizmos) yield break;

        // ── Dev gizmos ───────────────────────────────────────────────────────

        // DEV: Assign to commander
        yield return new Command_Action
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
                    string label2 = tracker.HasBandwidthFor()
                        ? pawn.LabelCap
                        : $"{pawn.LabelCap} (bandwidth full)";
                    options.Add(new FloatMenuOption(label2, () =>
                    {
                        try
                        {
                            if (pawn?.relations == null || __instance?.relations == null)
                            {
                                Messages.Message("Pawn relations not ready.", MessageTypeDefOf.RejectInput, false);
                                return;
                            }

                            if (__instance.Faction != pawn.Faction)
                                __instance.SetFaction(pawn.Faction);

                            tracker.BindMech(__instance);

                            Pawn old = __instance.GetOverseer();
                            old?.relations?.TryRemoveDirectRelation(PawnRelationDefOf.Overseer, __instance);
                            pawn.relations.TryRemoveDirectRelation(PawnRelationDefOf.Overseer, __instance);
                            __instance.relations.TryRemoveDirectRelation(PawnRelationDefOf.Overseer, pawn);
                            pawn.relations.AddDirectRelation(PawnRelationDefOf.Overseer, __instance);
                            int used = (int)tracker.BandwidthUsed;
                            int max = (int)tracker.BandwidthMax;
                            Messages.Message(
                                $"{__instance.LabelCap} assigned to {pawn.LabelCap}. Bandwidth: {used}/{max}.",
                                MessageTypeDefOf.TaskCompletion,
                                false);
                        }
                        catch (System.Exception ex)
                        {
                            Log.Warning($"Necron assign to commander: {ex}");
                            Messages.Message("Could not assign overseer relation (see log).", MessageTypeDefOf.RejectInput, false);
                        }
                    }));
                }
                if (options.Count == 0)
                    options.Add(new FloatMenuOption("No commanders on map", null));
                Find.WindowStack.Add(new FloatMenu(options));
            }
        };

        // DEV: Remove from commander
        yield return new Command_Action
        {
            defaultLabel = "DEV: Remove from commander",
            defaultDesc  = "Unbind this construct from its current commander.",
            action       = () =>
            {
                Pawn overseer = __instance.GetOverseer();
                if (overseer != null)
                {
                    HediffComp_NecronCommandTracker.GetTracker(overseer)?.UnbindMech(__instance);
                    overseer.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, __instance);
                    Messages.Message($"{__instance.LabelCap} unbound from {overseer.LabelCap}.", MessageTypeDefOf.NeutralEvent, false);
                }
                else
                {
                    Messages.Message($"{__instance.LabelCap} has no commander.", MessageTypeDefOf.RejectInput, false);
                }
            }
        };

        // DEV: Construct energy +5%
        yield return new Command_Action
        {
            defaultLabel = "DEV: Construct energy +5%",
            defaultDesc  = "Increase this construct's need levels by 5%.",
            action       = () =>
            {
                foreach (Need need in __instance.needs?.AllNeeds ?? Enumerable.Empty<Need>())
                    need.CurLevel = need.CurLevel + 0.05f;
            }
        };

        // DEV: Construct energy -5%
        yield return new Command_Action
        {
            defaultLabel = "DEV: Construct energy -5%",
            defaultDesc  = "Decrease this construct's need levels by 5%.",
            action       = () =>
            {
                foreach (Need need in __instance.needs?.AllNeeds ?? Enumerable.Empty<Need>())
                    need.CurLevel = need.CurLevel - 0.05f;
            }
        };
    }
}
