using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

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
    private Sustainer eatSustainer;

    public CompProperties_UseEffectNecrodermisPackConsume PropsTyped => (CompProperties_UseEffectNecrodermisPackConsume)props;

    public override void PrepareTick()
    {
        base.PrepareTick();
        Pawn user = PawnUsingParentForUseItem();
        if (user == null)
        {
            EndEatSound();
            return;
        }

        SoundDef ingestSound = DefDatabase<SoundDef>.GetNamedSilentFail("Meal_Eat");
        if (eatSustainer == null || eatSustainer.def != ingestSound)
        {
            EndEatSound();
            if (ingestSound != null && ingestSound.sustain)
                eatSustainer = ingestSound.TrySpawnSustainer(SoundInfo.InMap(user, MaintenanceType.PerTick));
        }

        eatSustainer?.Maintain();
    }

    public override void DoEffect(Pawn user)
    {
        try
        {
            base.DoEffect(user);
            if (parent.Destroyed || user?.needs?.TryGetNeed<Need_Necrodermis>() == null)
                return;
            if (NecrodermisIngestionUtility.IsCanoptek(user))
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
        finally
        {
            EndEatSound();
        }
    }

    private Pawn PawnUsingParentForUseItem()
    {
        Map map = parent.Map;
        if (map == null)
            return null;
        foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
        {
            if (pawn.jobs?.curDriver is not JobDriver_UseItem)
                continue;
            if (pawn.CurJob?.GetTarget(TargetIndex.A).Thing == parent)
                return pawn;
        }
        return null;
    }

    private void EndEatSound()
    {
        eatSustainer?.End();
        eatSustainer = null;
    }
}
