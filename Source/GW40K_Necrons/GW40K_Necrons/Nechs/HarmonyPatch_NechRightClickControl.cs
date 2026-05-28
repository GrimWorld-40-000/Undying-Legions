using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

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
        HediffComp_ControlNodeTracker controlNodeTracker = HediffComp_ControlNodeTracker.GetTracker(controller);
        if (tracker == null && controlNodeTracker == null)
            return;

        for (int i = 0; i < context.ClickedThings.Count; i++)
        {
            if (context.ClickedThings[i] is not Pawn target)
                continue;
            if (!NechTakeControlJobUtility.IsFriendlyTakeControlTarget(controller, target))
                continue;
            if (!controller.CanReach(target, PathEndMode.Touch, Danger.Deadly))
                continue;

            // Spyder/old nechs: ThingDef carries NecronMechExtension.
            // Pawn-based Nech soldiers are explicitly whitelisted via NechUtility helper.
            // Canoptek (scarab swarms, etc.) stays on the Control Node branch, never Command Protocol BindMech.
            bool hasMechExtension = target.def.GetModExtension<NecronMechExtension>() != null;
            bool isCanoptek = ControlNodeUtility.IsCanoptek(target);
            bool isPawnHumanlikeNechSoldier = NechUtility.IsHumanlikeNechControlled(target);
            bool isNechConstruct = hasMechExtension || isPawnHumanlikeNechSoldier;

            if (isNechConstruct && tracker != null)
            {
                bool commandedByController = tracker.controlledMechs != null
                    && tracker.controlledMechs.Contains(target);
                if (commandedByController)
                {
                    string releaseLabel = "GW40K_ReleaseCommandOfNech".Translate(target.LabelShortCap);
                    __result.Add(FloatMenuUtility.DecoratePrioritizedTask(
                        new FloatMenuOption(releaseLabel, delegate
                        {
                            tracker.UnbindMech(target);
                            Messages.Message("GW40K_ReleaseCommandSuccess".Translate(target.LabelShortCap), MessageTypeDefOf.TaskCompletion, false);
                        }),
                        controller,
                        target));
                    break;
                }
                if (HediffComp_NecronCommandTracker.GetCommanderOf(target) != null)
                    continue;

                string optionLabel = "GW40K_TakeCommandOfNech".Translate(target.LabelShortCap);
                __result.Add(FloatMenuUtility.DecoratePrioritizedTask(
                    new FloatMenuOption(optionLabel, delegate
                    {
                        if (!NechTakeControlJobUtility.IsFriendlyTakeControlTarget(controller, target))
                        {
                            Messages.Message("GW40K_TakeControlNotFriendly".Translate(), MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        if (HediffComp_NecronCommandTracker.GetCommanderOf(target) != null)
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

            if (isCanoptek && controlNodeTracker != null)
            {
                bool controllerIsSpyder = ControlNodeUtility.IsSpyder(controller);
                bool controllerIsUncontrolled = HediffComp_NecronCommandTracker.GetCommanderOf(controller) == null;
                if (controllerIsSpyder && controllerIsUncontrolled)
                    continue;

                bool connectedByController = controlNodeTracker.controlledScarabs != null
                    && controlNodeTracker.controlledScarabs.Contains(target);
                if (connectedByController)
                {
                    string releaseLabel = "GW40K_ControlNodeDisconnectScarab".Translate(target.LabelShortCap);
                    __result.Add(FloatMenuUtility.DecoratePrioritizedTask(
                        new FloatMenuOption(releaseLabel, delegate
                        {
                            controlNodeTracker.UnbindScarab(target);
                            Messages.Message("GW40K_ControlNodeDisconnectSuccess".Translate(target.LabelShortCap), MessageTypeDefOf.TaskCompletion, false);
                        }),
                        controller,
                        target));
                    break;
                }

                if (HediffComp_ControlNodeTracker.GetControllerOfScarab(target) != null)
                    continue;

                string optionLabel = "GW40K_ControlNodeConnectScarab".Translate(target.LabelShortCap);
                __result.Add(FloatMenuUtility.DecoratePrioritizedTask(
                    new FloatMenuOption(optionLabel, delegate
                    {
                        if (!NechTakeControlJobUtility.IsFriendlyTakeControlTarget(controller, target))
                        {
                            Messages.Message("GW40K_TakeControlNotFriendly".Translate(), MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        if (HediffComp_ControlNodeTracker.GetControllerOfScarab(target) != null)
                        {
                            Messages.Message("GW40K_NechAlreadyCommanded".Translate(target.LabelShortCap), MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        if (!controlNodeTracker.HasBandwidthFor(target))
                        {
                            Messages.Message("GW40K_ControlNodeBandwidthFull".Translate(), MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        if (!controlNodeTracker.IsWithinControlRange(target))
                        {
                            Messages.Message(
                                "GW40K_CommandOutOfRange".Translate(controlNodeTracker.ControlRange.ToString("0.#")),
                                MessageTypeDefOf.RejectInput,
                                false);
                            return;
                        }
                        if (!controlNodeTracker.BindScarab(target))
                            return;
                        SoundDef complete = DefDatabase<SoundDef>.GetNamedSilentFail("ControlMech_Complete");
                        if (complete != null && target.MapHeld != null)
                            complete.PlayOneShot(SoundInfo.InMap(target));
                        else if (target.MapHeld != null)
                            SoundDefOf.Tick_Tiny.PlayOneShot(SoundInfo.InMap(target));
                        Messages.Message(
                            "GW40K_ControlNodeConnectSuccess".Translate(controller.LabelShortCap, target.LabelShortCap),
                            MessageTypeDefOf.PositiveEvent,
                            true);
                    }),
                    controller,
                    target));
                break;
            }
        }
    }
}
