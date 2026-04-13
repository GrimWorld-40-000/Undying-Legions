using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace GW40K_Necrons;

// Strips Nech pawns from the vanilla mech damage and threat alerts so they
// don't pollute the alert panel. Custom Nech-specific alerts replace them.

[HarmonyPatch(typeof(Alert_MechDamaged), "GetReport")]
public static class HarmonyPatch_SuppressNechAlert_Damaged
{
    public static void Postfix(ref AlertReport __result) =>
        NechAlertFilter.FilterNechs(ref __result);
}

[HarmonyPatch(typeof(Alert_MechMissingBodyPart), "GetReport")]
public static class HarmonyPatch_SuppressNechAlert_MissingPart
{
    public static void Postfix(ref AlertReport __result) =>
        NechAlertFilter.FilterNechs(ref __result);
}

[HarmonyPatch(typeof(Alert_MechThreat), "GetReport")]
public static class HarmonyPatch_SuppressNechAlert_Threat
{
    public static void Postfix(ref AlertReport __result) =>
        NechAlertFilter.FilterNechs(ref __result);
}

internal static class NechAlertFilter
{
    internal static void FilterNechs(ref AlertReport report)
    {
        if (!report.active) return;

        List<GlobalTargetInfo> culprits = report.culpritsTargets;
        if (culprits == null || culprits.Count == 0) return;

        culprits.RemoveAll(t => t.Thing is Pawn p
            && p.def.GetModExtension<NecronMechExtension>() != null);

        if (culprits.Count == 0)
            report = AlertReport.Inactive;
    }
}
