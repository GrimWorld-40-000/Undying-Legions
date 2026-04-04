using RimWorld;
using Verse;

#nullable disable
namespace NecronComp;

public class IngestionOutcomeDoer_GiveHediffNecron : IngestionOutcomeDoer
{
    public HediffDef hediffDef;
    public HediffDef hediffWhenRaceMatch;
    public float severity = 0.5f;
    public float severityWhenMatch = 0.1f;
    public System.Collections.Generic.List<ThingDef> raceException = new System.Collections.Generic.List<ThingDef>();

    protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount) { }
}
