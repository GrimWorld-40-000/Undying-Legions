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
        bool hasEternalSlumberGene = ___pawn.genes?.GenesListForReading?.Any(g => g?.def?.defName == "GW_UD_EternalSlumber") == true;
        bool isNecronNeedProfile = nonOrganic != null || hasEternalSlumberGene;
        bool isNecronPawn = NechEnergyUtility.IsNecronPawn(___pawn);

        // Ensure Necron profile pawns always use core flux instead of rest, even if generation path misses gene-driven need enabling.
        if (nd.defName == "GW40K_CoreFlux")
        {
            if (isNech)
            {
                __result = true;
                return;
            }
            __result = isNecronNeedProfile;
        }
        if (nd.defName == "GW_UD_Necrodermis" && isNech)
            __result = true;
        if (nd.defName == "GW40K_NechEnergy")
        {
            __result = NechEnergyUtility.GetCapacitorComp(___pawn) != null;
        }
        if (nd.defName == "MechEnergy" && isNecronPawn)
            __result = false;
        if (nd.defName == "Rest" && isNecronNeedProfile)
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
