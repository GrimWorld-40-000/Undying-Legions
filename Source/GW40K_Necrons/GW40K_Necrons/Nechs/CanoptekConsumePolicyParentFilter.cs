using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Upper bound for the scarab Consume <see cref="ThingFilter"/>: what may ever appear in the UI.
/// Child filter (on <see cref="ThingComp_CanoptekConsumePolicy"/>) can only allow subsets of this.
/// </summary>
[StaticConstructorOnStartup]
public static class CanoptekConsumePolicyParentFilter
{
    public static readonly ThingFilter Instance = Build();

    private static ThingFilter Build()
    {
        ThingFilter f = new ThingFilter();
        f.ResolveReferences();
        f.SetDisallowAll();

        // Raw resources — ResourcesRaw minus explicit exclusions (necrodermis stack, gravlite panels, etc.).
        List<ThingDef> rawExceptions = RawResourceExceptions();
        if (ThingCategoryDefOf.ResourcesRaw != null)
            f.SetAllow(ThingCategoryDefOf.ResourcesRaw, true, rawExceptions, null);

        // Chunks are not always exposed under ResourcesRaw in filter UIs; allow explicitly.
        ThingCategoryDef chunks =
            ThingCategoryDefOf.Chunks
            ?? DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Chunks");
        if (chunks != null)
            f.SetAllow(chunks, true);

        // Fallback: ensure common vanilla chunk defs show even if category wiring differs.
        foreach (string defName in new[]
                 {
                     "ChunkGranite", "ChunkLimestone", "ChunkSandstone", "ChunkMarble", "ChunkSlate",
                     "ChunkSlagSteel", "SlagRubble"
                 })
        {
            ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (td != null)
                f.SetAllow(td, true);
        }

        if (ThingCategoryDefOf.Weapons != null)
            f.SetAllow(ThingCategoryDefOf.Weapons, true);
        if (ThingCategoryDefOf.Apparel != null)
            f.SetAllow(ThingCategoryDefOf.Apparel, true);
        if (ThingCategoryDefOf.Corpses != null)
            f.SetAllow(ThingCategoryDefOf.Corpses, true);

        // Plants — category defName varies by version; first match wins.
        ThingCategoryDef plants =
            DefDatabase<ThingCategoryDef>.GetNamedSilentFail("PlantMatterRaw")
            ?? DefDatabase<ThingCategoryDef>.GetNamedSilentFail("PlantsRaw")
            ?? DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Plants")
            ?? DefDatabase<ThingCategoryDef>.GetNamedSilentFail("FoodsRawPlant");
        if (plants != null)
            f.SetAllow(plants, true);

        // Manufactured — only textiles (cloth, synthread, etc.).
        ThingCategoryDef textiles =
            DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Textiles")
            ?? DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Fabrics");
        if (textiles != null)
            f.SetAllow(textiles, true);
        // Keep premium textiles visible even if category wiring changes in modded environments.
        foreach (string defName in new[] { "Cloth_Devilstrand", "Synthread", "Hyperweave" })
        {
            ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (td != null)
                f.SetAllow(td, true);
        }

        // Plascrete / Rockcrete (Odyssey etc.) — allow by ThingDef if present.
        foreach (string defName in new[] { "Plascrete", "Rockcrete", "BlocksPlascrete", "BlocksRockcrete" })
        {
            ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (td != null)
                f.SetAllow(td, true);
        }

        return f;
    }

    /// <summary>ThingDefs under ResourcesRaw that must never appear in the Consume UI.</summary>
    private static List<ThingDef> RawResourceExceptions()
    {
        List<ThingDef> list = new List<ThingDef>();
        AddDefIfPresent(list, "GW40k_Necron_Necrodermis");

        ThingCategoryDef raw = ThingCategoryDefOf.ResourcesRaw;
        if (raw != null)
        {
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (d?.defName == null || !IsUnderThingCategory(d, raw))
                    continue;
                if (d.defName.IndexOf("Gravlite", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddDefIfPresent(list, d.defName);
            }
        }

        return list;
    }

    private static bool IsUnderThingCategory(ThingDef def, ThingCategoryDef root)
    {
        if (def.thingCategories == null)
            return false;
        for (int i = 0; i < def.thingCategories.Count; i++)
        {
            ThingCategoryDef c = def.thingCategories[i];
            for (ThingCategoryDef cur = c; cur != null; cur = cur.parent)
            {
                if (cur == root)
                    return true;
            }
        }

        return false;
    }

    private static void AddDefIfPresent(List<ThingDef> list, string defName)
    {
        ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        if (d != null && !list.Contains(d))
            list.Add(d);
    }
}
