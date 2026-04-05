using HarmonyLib;
using RimWorld;
using System.Reflection;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Replaces the bandwidth gizmo filled-block color with Necrodermis green
/// when the mechanitor is a Necron Overlord (has Command Protocol).
/// Uses prefix/postfix to temporarily swap the static readonly field value.
/// </summary>
[HarmonyPatch(typeof(MechanitorBandwidthGizmo), "GizmoOnGUI")]
public static class HarmonyPatch_BandwidthColor
{
    private static readonly Color NecronGreen = new Color(168f / 255f, 211f / 255f, 160f / 255f, 1f);
    private static readonly FieldInfo FilledField = typeof(MechanitorBandwidthGizmo)
        .GetField("FilledBlockColor", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo TrackerField = typeof(MechanitorBandwidthGizmo)
        .GetField("tracker", BindingFlags.NonPublic | BindingFlags.Instance);

    private static Color originalFilled;
    private static bool swapped = false;

    [HarmonyPrefix]
    public static void Prefix(MechanitorBandwidthGizmo __instance)
    {
        swapped = false;
        if (FilledField == null || TrackerField == null) return;

        var tracker = TrackerField.GetValue(__instance) as Pawn_MechanitorTracker;
        if (tracker?.Pawn == null) return;

        if (tracker.Pawn.health?.hediffSet?.GetFirstHediffOfDef(
            HediffDef.Named("GW40K_CommandProtocolImplant")) == null) return;

        originalFilled = (Color)FilledField.GetValue(null);
        FilledField.SetValue(null, NecronGreen);
        swapped = true;
    }

    [HarmonyPostfix]
    public static void Postfix()
    {
        if (swapped && FilledField != null)
            FilledField.SetValue(null, originalFilled);
        swapped = false;
    }
}
