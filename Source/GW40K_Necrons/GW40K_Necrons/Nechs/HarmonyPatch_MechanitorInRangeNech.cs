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
        if (tr == null)
        {
            __result = false;
            return false;
        }

        if (!target.IsValid)
        {
            // No specific target — vanilla uses this as a "is mech commandable?" guard (e.g. verb gizmo
            // availability and draft-ability). For any properly-commanded Nech we return true here so that
            // verb gizmos and float-menu options are visible and enabled. Range is enforced separately in
            // HarmonyPatch_NechOrderedJobRange when the job is actually issued — double-enforcing it here
            // just disables gizmos with a confusing vanilla "not player controlled" message.
            __result = true;
            return false;
        }

        // Specific target (e.g. right-click attack cell): use commander→target distance so that the
        // float-menu disabled reason matches our range expectation.
        IntVec3 cell = target.HasThing ? target.Thing.Position : target.Cell;
        __result = overseer.Position.DistanceTo(cell) <= tr.ControlRange;
        return false;
    }
}
