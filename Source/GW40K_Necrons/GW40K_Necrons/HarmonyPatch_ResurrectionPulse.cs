using HarmonyLib;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Draws a pulsing green overlay directly over the pawn body while
/// GW40K_Necron_ResurrectionActive is present, indicating active resurrection.
/// </summary>
[HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
[HarmonyPatch(new[] { typeof(Vector3), typeof(Rot4?), typeof(bool) })]
[HarmonyPriority(Priority.Low)]
public static class HarmonyPatch_ResurrectionPulse
{
    [HarmonyPostfix]
    public static void Postfix(PawnRenderer __instance, Vector3 drawLoc)
    {
        if (NecronDefOfs.GW40K_Necron_ResurrectionActive == null)
            return;

        Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
        if (pawn?.health?.hediffSet == null)
            return;

        if (!pawn.health.hediffSet.HasHediff(NecronDefOfs.GW40K_Necron_ResurrectionActive))
            return;

        ResurrectionProtocolVisuals.DrawPawnPulse(drawLoc);
    }
}
