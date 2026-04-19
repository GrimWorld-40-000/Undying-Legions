using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Hides the fight/flee/ignore widget for Necrons.
/// Attack behaviour is handled by JobGiver_NecronAutoAttack in the think tree instead.
/// </summary>
[HarmonyPatch(typeof(Pawn_PlayerSettings), "get_UsesConfigurableHostilityResponse")]
public static class HarmonyPatch_NecronThreatBehavior
{
    [HarmonyPostfix]
    public static void Postfix(Pawn_PlayerSettings __instance, ref bool __result)
    {
        Pawn pawn = Traverse.Create((object)__instance).Field("pawn").GetValue<Pawn>();
        if (pawn?.def.GetModExtension<NonOrganicPawn>() == null)
            return;
        __result = false;
    }
}
