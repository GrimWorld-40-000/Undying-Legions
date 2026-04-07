using RimWorld;
using UnityEngine;
using Verse;

namespace NecronMod
{
    /// <summary>
    /// Humanlike mood (and optional colonization roll) when exposed to necrodermis outside the Necron need.
    /// Used on the injector with <c>colonizationChance</c> 0 (growth comes from another outcome doer); can be used on ingestibles with a chance roll.
    /// </summary>
    public class IngestionOutcomeDoer_HumanRawNecrodermis : IngestionOutcomeDoer
    {
        public ThoughtDef thoughtDef;
        public HediffDef colonizationHediffDef;
        public float colonizationChance = 0.04f;
        public NeedDef skipIfHasNeed;

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike)
                return;
            if (skipIfHasNeed != null && pawn.needs?.TryGetNeed(skipIfHasNeed) != null)
                return;

            if (thoughtDef != null && pawn.needs?.mood?.thoughts?.memories != null)
                pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);

            if (colonizationHediffDef == null || colonizationChance <= 0f)
                return;

            int n = Mathf.Max(1, ingestedCount);
            float atLeastOnce = 1f - Mathf.Pow(1f - colonizationChance, n);
            if (!Rand.Chance(atLeastOnce))
                return;

            Hediff existing = pawn.health?.hediffSet?.GetFirstHediffOfDef(colonizationHediffDef);
            if (existing != null)
                HealthUtility.AdjustSeverity(pawn, colonizationHediffDef, 0.04f);
            else
                pawn.health.AddHediff(HediffMaker.MakeHediff(colonizationHediffDef, pawn));
        }
    }
}
