using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Master list of traits blocked from all Necrons (NonOrganicPawn).
/// Add a defName string to BlockedTraitDefNames to extend the list.
/// </summary>
[HarmonyPatch(typeof(TraitSet), nameof(TraitSet.GainTrait))]
public static class HarmonyPatch_BlockTraits
{
    private static readonly HashSet<string> BlockedTraitDefNames = new HashSet<string>
    {
        "DrugDesire",   // Chemical Interest / Chemical Fascination — no biochemistry
    };

    [HarmonyPrefix]
    public static bool Prefix(TraitSet __instance, Trait trait)
    {
        if (!BlockedTraitDefNames.Contains(trait.def.defName))
            return true;

        Pawn pawn = Traverse.Create((object)__instance).Field("pawn").GetValue<Pawn>();
        if (pawn?.def.GetModExtension<NonOrganicPawn>() == null)
            return true;

        return false;
    }
}
