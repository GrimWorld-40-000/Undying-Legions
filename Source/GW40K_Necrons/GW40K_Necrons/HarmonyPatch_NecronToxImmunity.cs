using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

// Necron bodies are inorganic — no respiratory system or organic tissue to poison.
// GasUtility.IsAffectedByExposure gates all tox gas / rot stink exposure; returning
// false here makes the game treat Necron pawns as fully immune to atmospheric gases.

[HarmonyPatch(typeof(GasUtility), nameof(GasUtility.IsAffectedByExposure))]
public static class HarmonyPatch_NecronToxImmunity
{
    public static void Postfix(Pawn pawn, ref bool __result)
    {
        if (!__result) return;
        if (!NechEnergyUtility.IsNecronPawn(pawn)) return;
        __result = false;
    }
}
