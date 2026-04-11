using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Draws the command range ring for the hovered bandwidth gizmo during the map
/// selection overlay phase (pre-render), so GenDraw calls land before the camera renders.
/// </summary>
[HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionOverlays))]
public static class HarmonyPatch_BandwidthRingDraw
{
    public static void Postfix()
    {
        HediffComp_NecronCommandTracker tracker = Gizmo_NecronBandwidth.HoveredTracker;
        if (tracker == null)
            return;
        Pawn commander = tracker.CommanderPawn;
        if (commander == null || !commander.Spawned || commander.MapHeld != Find.CurrentMap)
            return;
        GenDraw.DrawRadiusRing(commander.Position, tracker.ControlRange);
    }
}
