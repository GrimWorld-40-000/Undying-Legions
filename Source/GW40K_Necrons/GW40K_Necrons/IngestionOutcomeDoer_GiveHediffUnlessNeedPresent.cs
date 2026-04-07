using RimWorld;
using Verse;

namespace NecronMod
{
    /// <summary>
    /// Applies a hediff on ingest unless the pawn already has a given need (e.g. necrodermis body degradation for Necrons).
    /// </summary>
    public class IngestionOutcomeDoer_GiveHediffUnlessNeedPresent : IngestionOutcomeDoer
    {
        public HediffDef hediffDef;
        public float severity = 0.1f;
        public NeedDef skipIfPawnHasNeed;

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (skipIfPawnHasNeed != null && pawn.needs?.TryGetNeed(skipIfPawnHasNeed) != null)
                return;
            Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
            hediff.Severity = severity;
            pawn.health.AddHediff(hediff);
        }
    }
}
