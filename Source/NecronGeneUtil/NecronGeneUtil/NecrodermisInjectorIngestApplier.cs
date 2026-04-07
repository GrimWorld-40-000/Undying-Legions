using RimWorld;
using UnityEngine;
using Verse;

namespace NecronGeneUtil;

/// <summary>
/// Shared logic for <see cref="Thing.Ingested"/> on one injector unit plus applying returned nutrition to food or <see cref="Need_Necrodermis"/>.
/// Used by <see cref="CompUseEffect_NecrodermisInjectorIngest"/> and surgery.
/// </summary>
public static class NecrodermisInjectorIngestApplier
{
    public static void ApplySingleUnitToPawn(Pawn user, Thing unit)
    {
        if (user == null || unit == null || unit.Destroyed)
            return;

        float wanted = NutritionWantedFor(user);
        float gained = unit.Ingested(user, wanted);
        if (!user.Dead)
        {
            if (user.needs?.food != null)
                user.needs.food.CurLevel += gained;
            else
                NecrodermisIngestionUtility.ApplyNutritionToNecrodermis(user, gained);
        }

        user.records.AddTo(RecordDefOf.NutritionEaten, gained);
    }

    /// <summary>
    /// For surgery: ingredient item is consumed by the bill; only run ingest outcome + nutrition on the patient.
    /// </summary>
    public static void ApplyVirtualInjectorDose(Pawn user)
    {
        if (user == null || FMJ_DefOf.GW_NecrodermisInjector == null)
            return;
        Thing unit = ThingMaker.MakeThing(FMJ_DefOf.GW_NecrodermisInjector);
        unit.stackCount = 1;
        try
        {
            ApplySingleUnitToPawn(user, unit);
        }
        finally
        {
            if (!unit.Destroyed)
                unit.Destroy();
        }
    }

    private static float NutritionWantedFor(Pawn user)
    {
        if (user?.needs?.food != null)
            return Mathf.Max(0f, user.needs.food.NutritionWanted);
        return NecrodermisIngestionUtility.NutritionWantedForNecrodermis(user);
    }
}
