using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;

#nullable disable
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

        HediffDef target = PickDefaultCapacitor(__instance);
        if (target == null)
            return;
        if (__instance.health.hediffSet.HasHediff(target))
            return;

        RemoveExistingCapacitors(__instance);
        BodyPartRecord part = ResolveCapacitorPart(__instance);
        __instance.health.AddHediff(target, part);
        __instance.needs?.AddOrRemoveNeedsAsAppropriate();
        Need gauss = __instance.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy);
        if (gauss != null)
            gauss.CurLevel = 0.75f;
    }

    private static HediffDef PickDefaultCapacitor(Pawn p)
    {
        if (NechEnergyUtility.IsOverlord(p))
            return NechEnergyUtility.CapacitorLargeDef;
        if (NechEnergyUtility.IsScarab(p))
            return NechEnergyUtility.CapacitorMicroDef;
        return NechEnergyUtility.CapacitorSmallDef;
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

        BodyPartDef gaussPartDef = DefDatabase<BodyPartDef>.GetNamedSilentFail("GW40K_GaussCapacitor");
        if (gaussPartDef != null)
        {
            BodyPartRecord explicitPart = p.RaceProps.body.AllParts.FirstOrDefault(bp => bp.def == gaussPartDef);
            if (explicitPart != null)
                return explicitPart;
        }

        BodyPartRecord torso = p.health.hediffSet.GetNotMissingParts().FirstOrDefault(bp => bp.def == BodyPartDefOf.Torso);
        if (torso != null)
            return torso;

        return p.health.hediffSet.GetNotMissingParts().FirstOrDefault();
    }
}
