using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Re-enables the Crafting work tag for pawns bearing the Command Protocol implant.
/// NecronGeneUtil.dll applies GW30K_Trait_BaseNecron to all base Necrons, which disables
/// Crafting. This patch restores it specifically for Command Protocol bearers (Crypteks,
/// Overlords) so they can operate the Monolith's summoning and crafting bills.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.WorkTagIsDisabled))]
public static class HarmonyPatch_CommandProtocolWork
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance, WorkTags w, ref bool __result)
    {
        if (!__result) return;
        if ((w & WorkTags.Crafting) == 0) return;

        if (__instance.health?.hediffSet?.GetFirstHediffOfDef(
            HediffDef.Named("GW40K_CommandProtocolImplant")) != null)
        {
            __result = false;
        }
    }
}
