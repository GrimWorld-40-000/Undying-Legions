using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Injects a "Necrons" toggle button into the center of the mech tab's top bar.
/// Disabled (grayed, no-op) when the player has no Nech-controlled pawns, unless god mode is on.
/// </summary>
[HarmonyPatch(typeof(MainTabWindow_Mechs), nameof(MainTabWindow_Mechs.DoWindowContents))]
static class HarmonyPatch_MechTabNecronButton
{
    private const float BtnW = 200f;
    private const float BtnH = 31f;

    [HarmonyPostfix]
    static void Postfix(MainTabWindow_Mechs __instance, Rect rect)
    {
        Rect btnRect = new Rect(rect.width / 2f - BtnW / 2f, 2f, BtnW, BtnH);
        bool enabled = DebugSettings.godMode || HasPlayerNecrons();

        if (!enabled)
        {
            Color prev = GUI.color;
            GUI.color = Color.grey;
            Widgets.ButtonText(btnRect, "Necrons");
            GUI.color = prev;
            TooltipHandler.TipRegion(btnRect, "No Necrons in your possession.");
            return;
        }

        if (Window_NechPanel.IsOpen)
            Widgets.DrawHighlight(btnRect);

        if (Widgets.ButtonText(btnRect, "Necrons"))
        {
            Window_NechPanel.Toggle();
            // Close the mechs tab itself when the Necrons panel opens.
            if (Window_NechPanel.IsOpen)
                __instance.Close(doCloseSound: false);
        }
    }

    private static bool HasPlayerNecrons()
    {
        if (Find.Maps == null) return false;
        foreach (Map map in Find.Maps)
            foreach (Pawn p in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
                if (NechUtility.IsNechControlled(p))
                    return true;
        return false;
    }
}
