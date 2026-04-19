using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// DOUBLE-CHECK before modifying behavior!! Nechs without a command-protocol overseer cannot be drafted (blocks gizmo, hotkeys, and other callers).
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
        if (p == null || p.def.GetModExtension<NecronMechExtension>() == null)
            return true;
        if (HediffComp_NecronCommandTracker.GetCommanderOf(p) != null)
            return true;

        Messages.Message(
            "GW40K_NechCannotDraftUncontrolled".Translate(p.LabelShortCap),
            MessageTypeDefOf.RejectInput,
            false);
        return false;
    }
}
