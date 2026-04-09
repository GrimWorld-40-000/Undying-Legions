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

        Pawn overseer = mech.GetOverseer();
        if (overseer == null)
        {
            __result = false;
            return false;
        }

        HediffComp_NecronCommandTracker tr = HediffComp_NecronCommandTracker.GetTracker(overseer);
        if (tr == null || tr.controlledMechs == null || !tr.controlledMechs.Contains(mech))
        {
            __result = false;
            return false;
        }

        if (!target.IsValid)
        {
            __result = false;
            return false;
        }

        IntVec3 cell = target.HasThing ? target.Thing.Position : target.Cell;
        __result = overseer.Position.DistanceTo(cell) <= tr.ControlRange;
        return false;
    }
}
