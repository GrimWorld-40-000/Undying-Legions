using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// If Eternal Slumber is removed for any reason other than <see cref="MaintenanceNeed"/> clearing it after a
/// legitimate recovery (flux back / replenishing), trigger forced murderous rage — regardless of current flux.
/// </summary>
public class HediffCompProperties_EternalSlumberInterruption : HediffCompProperties
{
    public HediffCompProperties_EternalSlumberInterruption()
    {
        compClass = typeof(HediffComp_EternalSlumberInterruption);
    }
}

public class HediffComp_EternalSlumberInterruption : HediffComp
{
    private bool suppressMurderousRageForCleanRemoval;

    /// <summary>Called by <see cref="MaintenanceNeed"/> immediately before removing this hediff for a valid recovery.</summary>
    public void MarkCleanRemovalFromNeed()
    {
        suppressMurderousRageForCleanRemoval = true;
    }

    public override void CompPostPostRemoved()
    {
        base.CompPostPostRemoved();
        Pawn pawn = parent.pawn;
        if (pawn == null || pawn.Dead || !pawn.Spawned)
            return;

        if (suppressMurderousRageForCleanRemoval)
        {
            suppressMurderousRageForCleanRemoval = false;
            return;
        }

        Pawn target = MurderousRageMentalStateUtility.FindPawnToKill(pawn);
        if (target == null)
        {
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
                Messages.Message("GW40K_EternalSlumberInterruptedNoTarget".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NegativeEvent);
            return;
        }

        MentalStateDef rageDef = DefDatabase<MentalStateDef>.GetNamedSilentFail("MurderousRage");
        if (rageDef == null)
            return;

        string reason = "GW40K_EternalSlumberInterrupted".Translate(pawn.Named("PAWN")).Resolve();
        pawn.mindState.mentalStateHandler.TryStartMentalState(
            rageDef,
            reason,
            forced: true,
            forceWake: true,
            causedByMood: false,
            otherPawn: target);
    }
}
