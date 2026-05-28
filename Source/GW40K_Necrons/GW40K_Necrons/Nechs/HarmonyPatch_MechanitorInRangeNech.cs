using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Drafted-move float menu calls <see cref="MechanitorUtility.InMechanitorCommandRange"/> which assumes a vanilla
/// mechanitor; Nechs use Command Protocol only and NRE there. Use commander + Necron tracker instead.
/// </summary>
[HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
public static class HarmonyPatch_MechanitorInRangeNech
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn mech, LocalTargetInfo target, ref bool __result)
    {
        if (mech?.def?.GetModExtension<NecronMechExtension>() == null)
            return true;

        Pawn overseer = HediffComp_NecronCommandTracker.GetCommanderOf(mech);
        HediffComp_NecronCommandTracker tr = overseer != null ? HediffComp_NecronCommandTracker.GetTracker(overseer) : null;

        if (!target.IsValid)
        {
            // No specific target — used as "is mech commandable?" guard for gizmo/draft visibility.
            // Nechs with a commander are commandable; those without (canoptek, unlinked) are also
            // commandable since HarmonyPatch_NechOrderedJobRange handles the actual restriction.
            __result = true;
            return false;
        }

        // Specific target (move/attack destination): always return true so the float-menu option
        // is enabled and the job reaches HarmonyPatch_NechOrderedJobRange.
        // For Goto jobs, TryRedirectMoveToRangeEdge clamps the destination to the range edge instead
        // of rejecting it — giving the vanilla "move as close as possible" behaviour.
        // For attack jobs out of range, HarmonyPatch_NechOrderedJobRange still shows our message.
        __result = true;
        return false;
    }
}
