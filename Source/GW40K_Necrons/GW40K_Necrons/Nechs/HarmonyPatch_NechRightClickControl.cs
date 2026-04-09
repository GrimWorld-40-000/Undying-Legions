using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Adds a right-click float menu option for the Nechinator (Command Protocol) to take command of
/// unassigned Nechs on the map — same flow as vanilla mechanitor + uncontrolled mech, gated by <see cref="MechanitorUtility.CanControlMech"/>
/// after <see cref="HarmonyPatch_MechControl"/> allows Command Protocol without Mechlink.
/// </summary>
[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]
public static class HarmonyPatch_NechRightClickControl
{
    [HarmonyPostfix]
    public static void Postfix(ref List<FloatMenuOption> __result, ref FloatMenuContext context)
    {
        if (__result == null || context.FirstSelectedPawn == null || context.ClickedThings == null)
            return;
        if (!ModsConfig.BiotechActive)
            return;

        Pawn controller = context.FirstSelectedPawn;
        HediffComp_NecronCommandTracker tracker = HediffComp_NecronCommandTracker.GetTracker(controller);
        if (tracker == null)
            return;

        for (int i = 0; i < context.ClickedThings.Count; i++)
        {
            if (context.ClickedThings[i] is not Pawn target)
                continue;
            if (target.def.GetModExtension<NecronMechExtension>() == null)
                continue;
            if (target.Faction != controller.Faction)
                continue;
            if (!controller.CanReach(target, PathEndMode.Touch, Danger.Deadly))
                continue;

            Pawn overseer = target.GetOverseer();
            bool commandedByController = overseer == controller
                && tracker.controlledMechs != null
                && tracker.controlledMechs.Contains(target);
            if (commandedByController)
            {
                string releaseLabel = "GW40K_ReleaseCommandOfNech".Translate(target.LabelShortCap);
                __result.Add(FloatMenuUtility.DecoratePrioritizedTask(
                    new FloatMenuOption(releaseLabel, delegate
                    {
                        tracker.UnbindMech(target);
                        controller.relations?.TryRemoveDirectRelation(PawnRelationDefOf.Overseer, target);
                        target.relations?.TryRemoveDirectRelation(PawnRelationDefOf.Overseer, controller);
                        Messages.Message("GW40K_ReleaseCommandSuccess".Translate(target.LabelShortCap), MessageTypeDefOf.TaskCompletion, false);
                    }),
                    controller,
                    target));
                break;
            }
            if (overseer != null)
                continue;

            string optionLabel = "GW40K_TakeCommandOfNech".Translate(target.LabelShortCap);
            __result.Add(FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(optionLabel, delegate
                {
                    if (target.GetOverseer() != null)
                    {
                        Messages.Message("GW40K_NechAlreadyCommanded".Translate(target.LabelShortCap), MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    if (!tracker.HasBandwidthFor(target))
                    {
                        Messages.Message("GW40K_CommandBandwidthFull".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }

                    Job job = JobMaker.MakeJob(NecronDefOfs.GW40K_TakeControlOfNech, target);
                    controller.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }),
                controller,
                target));
            break;
        }
    }
}
