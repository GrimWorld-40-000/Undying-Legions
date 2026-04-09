using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Vanilla gene suppression uses <see cref="GeneUtility.Overrides"/> with display order; hair colour
/// can still win over our jaw/head genes. Force Necron construct head genes to override HairColor endogenes.
/// </summary>
[HarmonyPatch(typeof(GeneUtility), nameof(GeneUtility.Overrides))]
public static class HarmonyPatch_GeneUtility_NecronHeadOverridesHair
{
    private static bool IsNecronConstructHeadGene(GeneDef def) =>
        def?.defName != null
        && def.defName.StartsWith("GW_UD_Necron")
        && def.defName.EndsWith("_Head");

    private static bool IsHairColorEndogene(GeneDef def) =>
        def != null && def.endogeneCategory == EndogeneCategory.HairColor;

    [HarmonyPostfix]
    public static void Postfix(GeneDef gene, GeneDef other, ref bool __result)
    {
        if (gene == null || other == null)
            return;

        // gene overrides other?
        if (IsHairColorEndogene(gene) && IsNecronConstructHeadGene(other))
            __result = false;

        if (IsNecronConstructHeadGene(gene) && IsHairColorEndogene(other))
            __result = true;
    }
}
