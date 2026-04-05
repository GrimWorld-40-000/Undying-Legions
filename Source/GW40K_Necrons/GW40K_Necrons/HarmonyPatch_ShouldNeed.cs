using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
public class HarmonyPatch_ShouldNeed
{
    [HarmonyPostfix]
    public static void fix(NeedDef nd, Pawn ___pawn, ref bool __result)
    {
        if (___pawn?.def == null) return;
        NonOrganicPawn nonOrganic = ___pawn.def.GetModExtension<NonOrganicPawn>();

        if (nd.defName == "GW40K_CoreFlux")
            __result = nonOrganic != null;
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
