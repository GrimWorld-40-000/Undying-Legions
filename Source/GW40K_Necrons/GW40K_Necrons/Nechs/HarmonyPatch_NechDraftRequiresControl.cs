using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// DOUBLE-CHECK before modifying behavior!! Nechs without command authority cannot be drafted
/// (blocks gizmo, hotkeys, and other callers). Authority can come from Command Protocol or
/// a local Control Node tracker (e.g. Canoptek Spyder).
/// </summary>
[HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
public static class HarmonyPatch_NechDraftRequiresControl
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn_DraftController __instance, bool value)
    {
        if (!value)
            return true;

        Pawn p = __instance?.pawn;
        if (p == null || !NechUtility.IsNechControlled(p))
            return true;
        if (HediffComp_NecronCommandTracker.GetCommanderOf(p) != null)
            return true;
        if (HediffComp_ControlNodeTracker.GetTracker(p) != null)
            return true;

        Messages.Message(
            "GW40K_NechCannotDraftUncontrolled".Translate(p.LabelShortCap),
            MessageTypeDefOf.RejectInput,
            false);
        return false;
    }
}
