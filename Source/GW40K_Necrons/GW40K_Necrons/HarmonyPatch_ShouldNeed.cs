using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
public class HarmonyPatch_ShouldNeed
{
    /// <summary>
    /// Avoid vanilla drug/chemical need checks on non-organic pawns (prevents life-stage / story null churn during generation).
    /// </summary>
    [HarmonyPrefix]
    public static bool Prefix(NeedDef nd, Pawn ___pawn, ref bool __result)
    {
        if (___pawn?.def == null || nd == null)
            return true;
        if (___pawn.def.GetModExtension<NonOrganicPawn>() == null)
            return true;
        if (nd.defName != null && nd.defName.StartsWith("Chemical_"))
        {
            __result = false;
            return false;
        }

        return true;
    }

    [HarmonyPostfix]
    public static void fix(NeedDef nd, Pawn ___pawn, ref bool __result)
    {
        if (___pawn?.def == null) return;
        NonOrganicPawn nonOrganic = ___pawn.def.GetModExtension<NonOrganicPawn>();
        bool isNech = ___pawn.def.GetModExtension<NecronMechExtension>() != null;
        bool isCanoptek = ControlNodeUtility.IsCanoptek(___pawn);
        bool isFlayed = (___pawn.def?.defName?.IndexOf("FlayedOne", System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
            || (___pawn.kindDef?.defName?.IndexOf("FlayedOne", System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        bool hasEternalSlumberGene = ___pawn.genes?.GenesListForReading?.Any(g => g?.def?.defName == "GW_UD_EternalSlumber") == true;
        bool isNecronNeedProfile = nonOrganic != null || hasEternalSlumberGene;
        bool isNecronPawn = NechEnergyUtility.IsNecronPawn(___pawn);

        // Ensure Necron profile pawns always use core flux instead of rest, even if generation path misses gene-driven need enabling.
        if (nd.defName == "GW40K_CoreFlux")
        {
            if (isCanoptek)
            {
                __result = false;
                return;
            }
            if (isNech)
            {
                __result = true;
                return;
            }
            __result = isNecronNeedProfile;
        }
        if (nd.defName == "GW_UD_Necrodermis" && (isNech || isCanoptek))
            __result = true;
        // The gauss energy need belongs to the capacitor, not the weapon.
        // A pawn with a capacitor always needs the energy bar so it can siphon
        // cores and recharge — even when no gauss weapon is currently equipped.
        if (nd.defName == "GW40K_NechEnergy")
        {
            __result = NechEnergyUtility.GetCapacitorComp(___pawn) != null;
        }
        // High-tier Necrons use Necrodermis/CoreFlux/Gauss systems, not vanilla meals.
        if (nd.defName == "Food" && (isNecronNeedProfile || isCanoptek) && !isFlayed)
            __result = false;
        if (nd.defName == "MechEnergy" && isNecronPawn)
            __result = false;
        if (nd.defName == "Rest" && (isNecronNeedProfile || isCanoptek))
            __result = false;
        if (nd.defName == "Joy" && nonOrganic != null)
            __result = false;
        if (nd.defName == "Beauty" && nonOrganic != null)
            __result = false;
        if (nd.defName == "Comfort" && nonOrganic != null && !nonOrganic.comfortNeed)
            __result = false;
        if (nd.defName == "Outdoors" && nonOrganic != null)
            __result = false;
        if (nd.defName == "Indoors" && nonOrganic != null)
            __result = false;
        if (nd.defName == "Mood" && nonOrganic != null)
            __result = false;
    }
}
