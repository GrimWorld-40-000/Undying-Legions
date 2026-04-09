using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

public static class NechEnergyUtility
{
    public static HediffDef CapacitorMicroDef => DefDatabase<HediffDef>.GetNamedSilentFail("GW40K_GaussCapacitorMicro");
    public static HediffDef CapacitorSmallDef => DefDatabase<HediffDef>.GetNamedSilentFail("GW40K_GaussCapacitorSmall");
    public static HediffDef CapacitorLargeDef => DefDatabase<HediffDef>.GetNamedSilentFail("GW40K_GaussCapacitorLarge");

    public static bool IsNecronPawn(Pawn pawn)
    {
        if (pawn?.def == null)
            return false;
        if (pawn.def.GetModExtension<NecronMechExtension>() != null
            || pawn.def.GetModExtension<NonOrganicPawn>() != null)
            return true;
        string dn = pawn.def.defName ?? string.Empty;
        return dn.IndexOf("Necron", System.StringComparison.OrdinalIgnoreCase) >= 0
            || dn.IndexOf("Scarab", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsScarab(Pawn pawn) =>
        pawn?.def?.defName?.IndexOf("Scarab", System.StringComparison.OrdinalIgnoreCase) >= 0;

    public static bool IsOverlord(Pawn pawn) =>
        pawn?.def?.defName?.IndexOf("Overlord", System.StringComparison.OrdinalIgnoreCase) >= 0;

    public static float CoreSizeRaw(Pawn pawn)
    {
        if (pawn?.def == null)
            return 1f;
        NonOrganicPawn neo = pawn.def.GetModExtension<NonOrganicPawn>();
        if (neo != null)
            return neo.coreSize > 0f ? neo.coreSize : neo.eternalSlumberLengthFactor;
        NecronMechExtension mech = pawn.def.GetModExtension<NecronMechExtension>();
        if (mech != null)
            return mech.coreSize > 0f ? mech.coreSize : mech.eternalSlumberLengthFactor;
        return 1f;
    }

    public static float CoreFluxCapacityMultiplier(Pawn pawn)
    {
        float raw = CoreSizeRaw(pawn);
        if (raw <= 0.5f)
            return 0.5f;
        if (raw <= 1.01f)
            return 1f;
        if (raw <= 2.01f)
            return 1.5f;
        if (raw <= 3.01f)
            return 2f;
        return 3f;
    }

    public static HediffComp_GaussCapacitor GetCapacitorComp(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null)
            return null;
        if (CapacitorLargeDef != null)
        {
            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(CapacitorLargeDef);
            if (h != null)
                return h.TryGetComp<HediffComp_GaussCapacitor>();
        }
        if (CapacitorSmallDef != null)
        {
            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(CapacitorSmallDef);
            if (h != null)
                return h.TryGetComp<HediffComp_GaussCapacitor>();
        }
        if (CapacitorMicroDef != null)
        {
            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(CapacitorMicroDef);
            if (h != null)
                return h.TryGetComp<HediffComp_GaussCapacitor>();
        }
        return null;
    }

    public static float CapacitorCapacity(Pawn pawn)
    {
        return GetCapacitorComp(pawn)?.Props?.capacity ?? 0f;
    }

    public static float CoreFluxCostPerFullRecharge(Pawn pawn)
    {
        return GetCapacitorComp(pawn)?.Props?.coreFluxCostFull ?? 0.25f;
    }

    public static float CapacitorMass(Pawn pawn)
    {
        return GetCapacitorComp(pawn)?.Props?.mass ?? 0f;
    }

    public static bool AllowBatteryCharge(Pawn pawn)
    {
        return GetCapacitorComp(pawn)?.allowBatteryCharge ?? false;
    }

    public static bool AllowCoreRecharge(Pawn pawn)
    {
        return GetCapacitorComp(pawn)?.allowCoreCharge ?? true;
    }
}
