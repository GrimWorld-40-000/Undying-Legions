using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Vanilla <see cref="Designator_MechControlGroup.CanDesignateThing"/> assumes a mechanitor pipeline;
/// Necron constructs can NRE when selected. Disable this designator for Necron mechs.
/// </summary>
[HarmonyPatch(typeof(Designator_MechControlGroup), nameof(Designator_MechControlGroup.CanDesignateThing))]
public static class HarmonyPatch_DesignatorMechControlGroup
{
    [HarmonyPrefix]
    public static bool Prefix(Thing t, ref AcceptanceReport __result)
    {
        if (t is Pawn p && p.def.GetModExtension<NecronMechExtension>() != null)
        {
            __result = AcceptanceReport.WasRejected;
            return false;
        }

        return true;
    }
}
