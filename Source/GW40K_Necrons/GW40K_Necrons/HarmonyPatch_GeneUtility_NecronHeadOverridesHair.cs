using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Patches Gene.Active getter so that hair color genes are inactive on any pawn
/// that has an active Necron head gene. Sets overriddenByGene so the UI shows
/// "Overridden by gene: X" correctly.
/// </summary>
[HarmonyPatch(typeof(Gene), nameof(Gene.Active), MethodType.Getter)]
public static class HarmonyPatch_GeneUtility_NecronHeadOverridesHair
{
    private static bool IsNecronHeadGene(GeneDef def) =>
        def?.defName != null
        && def.defName.StartsWith("GW_UD_Necron")
        && def.defName.EndsWith("_Head");

    private static bool IsHairGene(GeneDef def) =>
        def != null
        && (def.endogeneCategory == EndogeneCategory.HairColor
            || def.hairColorOverride.HasValue
            || (def.exclusionTags != null && def.exclusionTags.Contains("HairColor")));

    [HarmonyPostfix]
    public static void Postfix(Gene __instance, ref bool __result)
    {
        // Only care about hair genes that are currently active
        if (!IsHairGene(__instance.def))
            return;

        if (__instance.pawn?.genes == null)
            return;

        List<Gene> genes = __instance.pawn.genes.GenesListForReading;
        for (int i = 0; i < genes.Count; i++)
        {
            Gene g = genes[i];
            if (!IsNecronHeadGene(g.def))
                continue;
            // Necron head gene is present and not itself suppressed
            if (g.overriddenByGene != null)
                continue;

            // Suppress this hair gene, set overriddenByGene for tooltip display
            __instance.overriddenByGene = g;
            __result = false;
            return;
        }

        // No active Necron head — clear any stale suppression we set
        if (__instance.overriddenByGene != null && IsNecronHeadGene(__instance.overriddenByGene.def))
            __instance.overriddenByGene = null;
    }
}
