using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// "The Sorrow" mental state: the Necron is lost in Soul-Debt immersion.
/// Each tick slowly drains the GW40K_Necron_RecallMemories hediff severity toward
/// <see cref="SeverityFloor"/>. The hediff is never removed — Soul-Debt does not leave,
/// it only goes quiet. Once the break ends, the hediff's +0.03/day SeverityPerDay comp
/// rebuilds it naturally until the next Immersion cycle.
/// Net drain ≈ (DrainPerDay - 0.03)/day ≈ 0.17/day → ~4 days from the 0.70 threshold.
/// </summary>
public class MentalState_TheSorrow : MentalState
{
    private const string RecallMemoriesDefName = "GW40K_Necron_RecallMemories";

    /// <summary>Gross drain rate (hediff comp still adds +0.03/day on top).</summary>
    private const float DrainPerDay = 0.20f;

    /// <summary>
    /// Severity is never drained below this value so the hediff persists in the
    /// Echoes stage and can rebuild when the break ends.
    /// </summary>
    private const float SeverityFloor = 0.05f;

    public override void MentalStateTick(int delta)
    {
        base.MentalStateTick(delta);

        Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(
            DefDatabase<HediffDef>.GetNamedSilentFail(RecallMemoriesDefName));

        if (hediff == null)
        {
            RecoverFromState();
            return;
        }

        hediff.Severity -= DrainPerDay / GenDate.TicksPerDay;

        if (hediff.Severity <= SeverityFloor)
        {
            hediff.Severity = SeverityFloor;
            RecoverFromState();
            TryImpartDysphorakh();
        }
    }

    /// <summary>
    /// 5% chance fired after the break ends naturally to permanently imprint Dysphorakh.
    /// Phantom-biological sensations can crystallise into a permanent condition when
    /// The Sorrow runs its full cycle and the mind surfaces with new scars.
    /// Only fires once — skipped if the pawn already has the gene.
    /// </summary>
    private void TryImpartDysphorakh()
    {
        if (!Rand.Chance(0.05f))
            return;
        if (pawn?.genes == null)
            return;

        GeneDef dysphorakhGene = DefDatabase<GeneDef>.GetNamedSilentFail("GW_UD_Dysphorakh");
        if (dysphorakhGene == null)
            return;
        if (pawn.genes.HasActiveGene(dysphorakhGene))
            return;

        // Xenogene — acquired during play, not innate
        pawn.genes.AddGene(dysphorakhGene, xenogene: true);

        // Give the pawn a grace period before the first episode can fire
        Hediff dysphorakhHediff = pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_Dysphorakh);
        dysphorakhHediff?.TryGetComp<HediffComp_DysphorakhEpisodes>()
            ?.SetInitialCooldown((int)(7f * GenDate.TicksPerDay));

        if (PawnUtility.ShouldSendNotificationAbout(pawn))
            Find.LetterStack.ReceiveLetter(
                "GW40K_TheSorrow_DysphorakhTitle".Translate(),
                "GW40K_TheSorrow_DysphorakhDesc".Translate(pawn.Named("PAWN")),
                LetterDefOf.NegativeEvent,
                pawn);
    }
}
