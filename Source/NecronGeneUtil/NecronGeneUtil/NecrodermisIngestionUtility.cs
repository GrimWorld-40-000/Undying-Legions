using RimWorld;
using UnityEngine;
using Verse;

namespace NecronGeneUtil;

/// <summary>
/// Mirrors vanilla food ingest math for pawns that use <see cref="Need_Necrodermis"/> instead of <see cref="Need_Food"/>.
/// </summary>
public static class NecrodermisIngestionUtility
{
    public static float NutritionWantedForNecrodermis(Pawn pawn)
    {
        var need = pawn.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null)
            return 0f;
        return Mathf.Max(0f, need.MaxLevel - need.CurLevel);
    }

    public static void ApplyNutritionToNecrodermis(Pawn pawn, float nutritionGained)
    {
        if (pawn.Dead || nutritionGained <= 0f)
            return;
        var need = pawn.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null)
            return;
        need.CurLevel = Mathf.Min(need.CurLevel + nutritionGained, need.MaxLevel);
    }
}
