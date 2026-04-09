using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Colony Necron bios (non-mech) with a gauss capacitor get the same Gauss energy gizmo as Nechs.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
[HarmonyPriority(Priority.Last)]
public static class HarmonyPatch_NecronColonistGaussGizmo
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
    {
        foreach (Gizmo g in gizmos)
            yield return g;

        if (__instance?.def?.GetModExtension<NecronMechExtension>() != null)
            yield break;
        if (__instance?.def?.GetModExtension<NonOrganicPawn>() == null)
            yield break;
        if (NechEnergyUtility.GetCapacitorComp(__instance) == null)
            yield break;
        if (__instance.Faction != Faction.OfPlayer)
            yield break;

        yield return new Gizmo_NechEnergy(__instance);
    }
}
