using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof(ColonistBar), "CheckRecacheEntries")]
public static class HarmonyPatch_HideNecronFromColonistBar
{
    [HarmonyPostfix]
    public static void Postfix(ColonistBar __instance)
    {
        List<ColonistBar.Entry> entries = Traverse.Create(__instance)
            .Field("cachedEntries")
            .GetValue<List<ColonistBar.Entry>>();

        entries?.RemoveAll(e => e.pawn?.def.GetModExtension<NecronMechExtension>() != null);
    }
}
