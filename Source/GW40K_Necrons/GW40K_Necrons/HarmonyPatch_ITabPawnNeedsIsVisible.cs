using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Nechs can have an empty <see cref="Pawn_NeedsTracker.AllNeeds"/> until the gauss capacitor hediff exists;
/// refresh needs when opening the Needs tab so gauss/core/necrodermis rows appear.
/// </summary>
[HarmonyPatch(typeof(ITab_Pawn_Needs))]
[HarmonyPatch(nameof(ITab_Pawn_Needs.IsVisible), MethodType.Getter)]
public static class HarmonyPatch_ITabPawnNeedsIsVisible
{
    [HarmonyPostfix]
    public static void Postfix(ITab_Pawn_Needs __instance, ref bool __result)
    {
        Pawn p = Traverse.Create(__instance).Property("SelPawn").GetValue<Pawn>();
        if (p == null)
            return;
        if (p.def.GetModExtension<NecronMechExtension>() == null)
            return;
        if (p.needs == null)
            return;
        if (__result)
            return;
        if (NechEnergyUtility.GetCapacitorComp(p) == null)
            return;

        p.needs.AddOrRemoveNeedsAsAppropriate();
        __result = p.needs.AllNeeds.Count > 0 && (!p.RaceProps.Animal || p.Faction != null);
    }
}
