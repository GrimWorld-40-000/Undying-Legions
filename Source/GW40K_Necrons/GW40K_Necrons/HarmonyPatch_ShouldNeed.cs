using HarmonyLib;
using RimWorld;
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

        // Core flux is gene-gated (Eternal Slumber → enablesNeeds). Only strip it from organics.
        if (nd.defName == "GW40K_CoreFlux")
            __result = __result && nonOrganic != null;
        if (nd.defName == "Rest" && nonOrganic != null)
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
