using RimWorld;
using UnityEngine;
using Verse;

namespace NecronGeneUtil;

/// <summary>
/// Necrons with <see cref="Need_Necrodermis"/> consume raw necrodermis packs via <see cref="CompUsable"/> (no <see cref="ThingDef.ingestible"/>).
/// </summary>
public class CompProperties_UseEffectNecrodermisPackConsume : CompProperties_UseEffect
{
    public float nutritionPerUnit = 0.05f;

    public CompProperties_UseEffectNecrodermisPackConsume()
    {
        compClass = typeof(CompUseEffect_NecrodermisPackConsume);
    }
}

public class CompUseEffect_NecrodermisPackConsume : CompUseEffect
{
    public CompProperties_UseEffectNecrodermisPackConsume PropsTyped => (CompProperties_UseEffectNecrodermisPackConsume)props;

    public override void DoEffect(Pawn user)
    {
        base.DoEffect(user);
        if (parent.Destroyed || user?.needs?.TryGetNeed<Need_Necrodermis>() == null)
            return;

        float wanted = NecrodermisIngestionUtility.NutritionWantedForNecrodermis(user);
        float per = Mathf.Max(0.001f, PropsTyped.nutritionPerUnit);
        int count = FoodUtility.StackCountForNutrition(wanted, per);
        count = Mathf.Clamp(count, 1, parent.stackCount);

        float total = 0f;
        for (int i = 0; i < count && !parent.Destroyed; i++)
        {
            parent.SplitOff(1);
            total += per;
        }

        if (total > 0f && !user.Dead)
            NecrodermisIngestionUtility.ApplyNutritionToNecrodermis(user, total);
        user.records.AddTo(RecordDefOf.NutritionEaten, total);
    }
}
