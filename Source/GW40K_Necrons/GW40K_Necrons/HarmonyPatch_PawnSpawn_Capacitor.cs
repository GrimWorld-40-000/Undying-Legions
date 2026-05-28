using HarmonyLib;
using NecronGeneUtil;
using RimWorld;
using System.Linq;
using Verse;

namespace GW40K_Necrons;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
public static class HarmonyPatch_PawnSpawn_Capacitor
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance)
    {
        if (!NechEnergyUtility.IsNecronPawn(__instance))
            return;
        if (__instance.health?.hediffSet == null)
            return;

        // MARKED FOR REMOVAL: EnsureFlayerVirus / EnsureDysphorakhForFlayed — only applied to nech-based UD_Necron_FlayedOne (removed).
        // EnsureFlayerVirus(__instance);
        // EnsureDysphorakhForFlayed(__instance);
        EnsureScarabNecrodermisFull(__instance);

        // Scarab swarms use necrodermis only; skip gauss capacitor and Spyder-specific work.
        if (__instance.def.defName == "GW40K_ScarabSwarm")
            return;

        EnsureSpyderControlNode(__instance);

        HediffDef target = PickDefaultCapacitor(__instance);
        if (target == null)
            return;

        if (!__instance.health.hediffSet.HasHediff(target))
        {
            RemoveExistingCapacitors(__instance);
            BodyPartRecord part = ResolveCapacitorPart(__instance);
            __instance.health.AddHediff(target, part);
        }

        // Always refresh needs — if the capacitor was already present (e.g. respawn after
        // save/load), the GW40K_NechEnergy need may not be in the needs list because the
        // early return previously skipped AddOrRemoveNeedsAsAppropriate.
        __instance.needs?.AddOrRemoveNeedsAsAppropriate();
        Need gauss = __instance.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy);
        if (gauss != null && gauss.CurLevel < gauss.MaxLevel * 0.01f)
            gauss.CurLevel = 1.0f;
    }

    // MARKED FOR REMOVAL — both methods only fired for nech-based UD_Necron_FlayedOne (removed).
    // private static void EnsureFlayerVirus(Pawn pawn)
    // {
    //     if (pawn?.def?.defName != "UD_Necron_FlayedOne") return;
    //     HediffDef virus = NecronDefOfs.GW40K_FlayerVirus;
    //     if (virus == null || pawn.health.hediffSet.HasHediff(virus)) return;
    //     pawn.health.AddHediff(virus);
    // }
    //
    // private static void EnsureDysphorakhForFlayed(Pawn pawn)
    // {
    //     if (pawn?.def?.defName != "UD_Necron_FlayedOne") return;
    //     HediffDef dysphorakh = NecronDefOfs.GW40K_Dysphorakh;
    //     if (dysphorakh == null || pawn.health.hediffSet.HasHediff(dysphorakh)) return;
    //     pawn.health.AddHediff(dysphorakh);
    // }

    private static void EnsureScarabNecrodermisFull(Pawn pawn)
    {
        if (pawn?.def?.defName != "GW40K_ScarabSwarm")
            return;
        pawn.needs?.AddOrRemoveNeedsAsAppropriate();
        Need_Necrodermis necro = pawn.needs?.TryGetNeed<Need_Necrodermis>();
        if (necro != null)
            necro.CurLevel = necro.MaxLevel;
    }

    private static HediffDef PickDefaultCapacitor(Pawn p)
    {
        string defName = p?.def?.defName;
        if (defName == "GW40K_NecronFlayedOne") // MARKED FOR REMOVAL: "UD_Necron_FlayedOne" removed (nech-based)
            return null;
        if (p.def == NecronDefOfs.UD_Necron_CanoptekSpyder)
            return NechEnergyUtility.CapacitorLargeDef;
        if (NechEnergyUtility.IsOverlord(p))
            return NechEnergyUtility.CapacitorLargeDef;
        if (NechEnergyUtility.IsCanoptek(p))
            return NechEnergyUtility.CapacitorMicroDef;
        return NechEnergyUtility.CapacitorSmallDef;
    }

    private static void EnsureSpyderControlNode(Pawn pawn)
    {
        if (pawn.def != NecronDefOfs.UD_Necron_CanoptekSpyder)
            return;
        HediffDef controlNodeDef = NecronDefOfs.GW40K_ControlNodeImplant;
        if (controlNodeDef == null || pawn.health.hediffSet.HasHediff(controlNodeDef))
            return;
        // AllParts is safe for fresh spawns; no missing-part filtering needed.
        BodyPartRecord head = pawn.RaceProps.body.AllParts.FirstOrDefault(bp => bp.def == BodyPartDefOf.Head);
        pawn.health.AddHediff(controlNodeDef, head);
    }

    private static void RemoveExistingCapacitors(Pawn p)
    {
        HediffSet hs = p.health.hediffSet;
        RemoveIfPresent(hs, NechEnergyUtility.CapacitorMicroDef);
        RemoveIfPresent(hs, NechEnergyUtility.CapacitorSmallDef);
        RemoveIfPresent(hs, NechEnergyUtility.CapacitorLargeDef);
    }

    private static void RemoveIfPresent(HediffSet hs, HediffDef def)
    {
        if (def == null)
            return;
        Hediff h = hs.GetFirstHediffOfDef(def);
        if (h != null)
            hs.pawn.health.RemoveHediff(h);
    }

    private static BodyPartRecord ResolveCapacitorPart(Pawn p)
    {
        if (p?.RaceProps?.body?.AllParts == null || p.health?.hediffSet == null)
            return null;

        BodyPartDef gaussPartDef = NecronDefOfs.GW40K_GaussCapacitor;
        if (gaussPartDef != null)
        {
            BodyPartRecord explicitPart = p.RaceProps.body.AllParts.FirstOrDefault(bp => bp.def == gaussPartDef);
            if (explicitPart != null)
                return explicitPart;
        }

        // AllParts avoids hediffSet filter overhead; torso missing on spawn is vanishingly rare.
        return p.RaceProps.body.AllParts.FirstOrDefault(bp => bp.def == BodyPartDefOf.Torso)
            ?? p.RaceProps.body.AllParts.FirstOrDefault();
    }
}
