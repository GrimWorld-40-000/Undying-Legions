using System;
using System.Collections.Generic;
using NecronGeneUtil;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Converts consumed material stacks into necrodermis bar fill for Canoptek constructs.
/// Call from ingest / haul / job completion after verifying <see cref="ThingComp_CanoptekConsumePolicy.consumeFilter"/> allows the thing.
/// </summary>
public static class CanoptekConsumeNecrodermisMath
{
    // --- Reference batches (gain = count * (gainPerBatch / batchSize)) ---
    private const float TextileBatch = 50f;
    private const float TextileGain = 0.19f;
    private const float PremiumTextileGain = 0.25f;

    private const float StoneBlockBatch = 40f;
    private const float StoneBlockGain = 0.25f;

    private const float ChunkOrSlagGainEach = 0.165f;

    private const float PreciousMetalBatch = 50f;
    private const float PreciousMetalGain = 0.5f;

    private const float BioferriteObsidianBatch = 50f;
    private const float BioferriteObsidianGain = 0.25f;

    private const float UraniumBatch = 50f;
    private const float UraniumGain = 0.4f;

    private const float WoodGainPerLog = 0.005f;

    private const float SteelBatch = 36f;
    private const float SteelGain = 0.25f;

    private const float IngestibleNutritionMultiplier = 0.15f;
    private const float NutritionMalus = 0.5f;
    private const float TreeFlatBonus = 0.01f;
    private const float SmeltReturnFraction = 0.10f;
    private const float WeaponMassGainPerSteelEquivalent = 0.07f;

    private static readonly HashSet<string> PremiumTextileDefNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cloth_Devilstrand",
        "Synthread",
        "Hyperweave"
    };

    private static readonly string[] PreciousMetalDefNames =
    {
        "Adamantium", "Auramite", "Ceramite", "GW_Adamantium", "GW_Auramite", "GW_Ceramite",
        "HP_Adamantium", "HP_Auramite", "HP_Ceramite"
    };

    private static readonly string[] BioferriteObsidianNames = { "Bioferrite", "Obsidian", "GW_Obsidian" };

    /// <summary>Total necrodermis bar fill from consuming <paramref name="count"/> of <paramref name="thing"/>.</summary>
    public static float GetNecrodermisGain(Thing thing, int count = 1)
    {
        if (thing == null || count <= 0)
            return 0f;
        // Plants use stackCount for internal/cluster state, not consumable stack size — always one plant.
        if (thing is Plant)
            count = 1;
        return GetNecrodermisGain(thing.def, thing.Stuff, count, thing);
    }

    /// <summary>Stack count for gain math: real stacks only; plants and non-stackables always 1.</summary>
    public static int EffectiveConsumeStackCount(Thing t)
    {
        if (t == null)
            return 1;
        if (t is Plant)
            return 1;
        int stackLimit = t.def?.stackLimit ?? 1;
        return stackLimit > 1 ? Mathf.Max(1, t.stackCount) : 1;
    }

    /// <summary>Abstract def path (no stuff, no stats from instance).</summary>
    public static float GetNecrodermisGain(ThingDef def, ThingDef stuff, int count = 1, Thing thing = null)
    {
        if (def == null || count <= 0)
            return 0f;

        // --- Textiles ---
        if (PremiumTextileDefNames.Contains(def.defName))
            return count * (PremiumTextileGain / TextileBatch);

        if (IsTextile(def))
            return count * (TextileGain / TextileBatch);

        if (IsStoneBlock(def))
            return count * (StoneBlockGain / StoneBlockBatch);

        if (IsStoneChunkOrSlag(def))
            return count * ChunkOrSlagGainEach;

        if (def == ThingDefOf.Steel)
            return count * (SteelGain / SteelBatch);

        if (IsAnyWood(def))
            return count * WoodGainPerUnit();

        if (def == ThingDefOf.Uranium)
            return count * (UraniumGain / UraniumBatch);

        if (def == ThingDefOf.Gold || def == ThingDefOf.Jade || def == ThingDefOf.Plasteel)
            return count * (PreciousMetalGain / PreciousMetalBatch);

        if (MatchesAnyDefName(def, PreciousMetalDefNames))
            return count * (PreciousMetalGain / PreciousMetalBatch);

        if (MatchesAnyDefName(def, BioferriteObsidianNames))
            return count * (BioferriteObsidianGain / BioferriteObsidianBatch);

        // Ingestibles with nutrition stat: nutrition * multiplier (same units as bar fill target).
        if (def.ingestible != null)
        {
            if (thing is Plant plant && IsTree(def))
            {
                float growth = Mathf.Clamp01(plant.Growth);
                float estimatedWoodYield = Mathf.Max(0f, GetTreeWoodYield(def));
                // Tree value follows the same wood conversion rate, with a small tree-specific bonus.
                // At full growth and harvestYield=60 this gives 60*(0.38/woodStackLimit)+0.01 ~= 0.314 (stackLimit=75).
                float treeGain = ((estimatedWoodYield * WoodGainPerUnit()) + TreeFlatBonus) * growth;
                return Mathf.Max(0f, treeGain);
            }

            // Live plants: instance nutrition can read very high vs abstract; scale by growth so wild grass is not a full meal.
            float nutritionPerUnit;
            if (thing is Plant plantForNutrition)
            {
                nutritionPerUnit = def.GetStatValueAbstract(StatDefOf.Nutrition, stuff) * Mathf.Clamp01(plantForNutrition.Growth);
            }
            else
            {
                nutritionPerUnit = thing != null
                    ? thing.GetStatValue(StatDefOf.Nutrition)
                    : def.GetStatValueAbstract(StatDefOf.Nutrition, stuff);
            }

            if (nutritionPerUnit > 0.0001f)
            {
                float gain = (nutritionPerUnit * NutritionMalus) * count * IngestibleNutritionMultiplier;
                return gain;
            }
        }

        if (def.IsWeapon || def.IsApparel)
            return WeaponOrApparelGain(def, stuff, count, thing);

        return 0f;
    }

    /// <summary>Apply gain if the pawn's consume filter allows this thing.</summary>
    public static void TryApplyGain(Pawn pawn, Thing thing, int count = 1)
    {
        if (pawn == null || thing == null || count <= 0 || !NecrodermisIngestionUtility.IsCanoptek(pawn))
            return;
        ThingFilter policy = ThingComp_CanoptekConsumePolicy.FilterFor(pawn);
        if (policy != null && !policy.Allows(thing))
            return;

        float gain = GetNecrodermisGain(thing, count);
        if (gain > 0f)
            NecrodermisIngestionUtility.ApplyNutritionToNecrodermis(pawn, gain);
    }

    private static bool IsTextile(ThingDef def)
    {
        if (def.thingCategories == null)
            return false;
        ThingCategoryDef textiles =
            DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Textiles")
            ?? DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Fabrics");
        if (textiles == null)
            return false;
        for (int i = 0; i < def.thingCategories.Count; i++)
        {
            ThingCategoryDef c = def.thingCategories[i];
            for (ThingCategoryDef cur = c; cur != null; cur = cur.parent)
            {
                if (cur == textiles)
                    return true;
            }
        }

        return false;
    }

    private static bool IsStoneBlock(ThingDef def)
    {
        ThingCategoryDef blocks = ThingCategoryDefOf.StoneBlocks
            ?? DefDatabase<ThingCategoryDef>.GetNamedSilentFail("StoneBlocks");
        return blocks != null && def.thingCategories != null && def.thingCategories.Contains(blocks);
    }

    private static bool IsStoneChunkOrSlag(ThingDef def)
    {
        string dn = def.defName ?? string.Empty;
        if (dn.IndexOf("Slag", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        ThingCategoryDef chunks = ThingCategoryDefOf.Chunks
            ?? DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Chunks");
        return IsUnderThingCategory(def, chunks);
    }

    private static bool IsAnyWood(ThingDef def)
    {
        if (def.thingCategories != null)
        {
            foreach (ThingCategoryDef c in def.thingCategories)
            {
                string n = c.defName ?? string.Empty;
                if (n.Equals("Woody", StringComparison.OrdinalIgnoreCase)
                    || n.Equals("WoodTypes", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        if (def.stuffProps?.categories != null)
        {
            StuffCategoryDef woody = DefDatabase<StuffCategoryDef>.GetNamedSilentFail("Woody");
            if (woody != null && def.stuffProps.categories.Contains(woody))
                return true;
        }

        return false;
    }

    private static float WoodGainPerUnit()
    {
        return WoodGainPerLog;
    }

    private static bool MatchesAnyDefName(ThingDef def, string[] names)
    {
        string dn = def.defName ?? string.Empty;
        foreach (string n in names)
        {
            if (dn.Equals(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsTree(ThingDef def)
    {
        if (def?.plant == null)
            return false;

        if (def.plant.IsTree)
            return true;

        string dn = def.defName ?? string.Empty;
        string label = def.label ?? string.Empty;
        return dn.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0
            || label.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static float GetTreeWoodYield(ThingDef def)
    {
        if (def?.plant == null)
            return 0f;

        if (def.plant.harvestedThingDef == ThingDefOf.WoodLog)
            return Mathf.Max(0f, def.plant.harvestYield);

        // Fallback for modded trees that don't declare wood logs as harvestedThingDef.
        string dn = def.defName ?? string.Empty;
        string label = def.label ?? string.Empty;
        if (dn.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0
            || label.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Mathf.Max(0f, def.plant.harvestYield);
        }

        return 0f;
    }

    private static bool IsUnderThingCategory(ThingDef def, ThingCategoryDef root)
    {
        if (def?.thingCategories == null || root == null)
            return false;

        for (int i = 0; i < def.thingCategories.Count; i++)
        {
            for (ThingCategoryDef cur = def.thingCategories[i]; cur != null; cur = cur.parent)
            {
                if (cur == root)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Weapons / apparel: no single canonical recipe across mods.
    /// Use steel-equivalent mass plus a recipe reclaim estimate (10% of recipe stuff/costs) to better reflect
    /// material value for dense/light stuffs like plasteel.
    /// </summary>
    private static float WeaponOrApparelGain(ThingDef def, ThingDef stuff, int count, Thing thing)
    {
        float massPerUnit = thing != null
            ? thing.GetStatValue(StatDefOf.Mass)
            : def.GetStatValueAbstract(StatDefOf.Mass, stuff);
        float steelMass = Mathf.Max(0.001f, ThingDefOf.Steel.GetStatValueAbstract(StatDefOf.Mass));
        float steelUnitsPerItem = massPerUnit / steelMass;
        float massGainPerItem = steelUnitsPerItem * WeaponMassGainPerSteelEquivalent;

        float reclaimGainPerItem = EstimateReclaimGainPerItem(def, stuff);
        float hpFactor = CurrentHitPointFactor(thing);
        return (massGainPerItem + reclaimGainPerItem) * Mathf.Max(1, count) * hpFactor;
    }

    private static float EstimateReclaimGainPerItem(ThingDef def, ThingDef stuff)
    {
        if (def == null)
            return 0f;

        float total = 0f;

        if (def.MadeFromStuff && stuff != null && def.costStuffCount > 0)
        {
            int reclaimedStuff = Mathf.FloorToInt(def.costStuffCount * SmeltReturnFraction);
            if (reclaimedStuff > 0)
                total += GetNecrodermisGain(stuff, null, reclaimedStuff, null);
        }

        if (def.costList != null)
        {
            for (int i = 0; i < def.costList.Count; i++)
            {
                ThingDefCountClass c = def.costList[i];
                if (c?.thingDef == null || c.count <= 0)
                    continue;
                int reclaimed = Mathf.FloorToInt(c.count * SmeltReturnFraction);
                if (reclaimed <= 0)
                    continue;
                total += GetNecrodermisGain(c.thingDef, null, reclaimed, null);
            }
        }

        return total;
    }

    private static float CurrentHitPointFactor(Thing thing)
    {
        if (thing == null || !thing.def.useHitPoints)
            return 1f;
        int maxHp = Mathf.Max(1, thing.MaxHitPoints);
        return Mathf.Clamp01(thing.HitPoints / (float)maxHp);
    }
}
