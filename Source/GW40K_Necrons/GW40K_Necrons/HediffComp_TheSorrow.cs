using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Properties for <see cref="HediffComp_TheSorrow"/>.
/// </summary>
public class HediffCompProperties_TheSorrow : HediffCompProperties
{
    /// <summary>Mean-time-between-events in days for triggering The Sorrow break.</summary>
    public float mtbDays = 9f;

    /// <summary>Minimum hediff severity before the break can trigger (Immersion stage threshold).</summary>
    public float minSeverity = 0.70f;

    public HediffCompProperties_TheSorrow()
    {
        compClass = typeof(HediffComp_TheSorrow);
    }
}

/// <summary>
/// Triggers the GW40K_TheSorrow mental state at MTB <see cref="HediffCompProperties_TheSorrow.mtbDays"/> days
/// while the pawn is in the Immersion stage (severity >= <see cref="HediffCompProperties_TheSorrow.minSeverity"/>).
/// Does nothing if the pawn is already in any mental state.
/// </summary>
public class HediffComp_TheSorrow : HediffComp
{
    private HediffCompProperties_TheSorrow Props => (HediffCompProperties_TheSorrow)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = parent.pawn;
        if (pawn == null || pawn.Dead || !pawn.Spawned)
            return;
        if (pawn.Downed || !pawn.Awake())
            return;
        if (parent.Severity < Props.minSeverity)
            return;
        if (pawn.MentalStateDef != null)
            return;

        if (!Rand.MTBEventOccurs(Props.mtbDays, GenDate.TicksPerDay, 1))
            return;

        MentalStateDef stateDef = NecronDefOfs.GW40K_TheSorrow;
        if (stateDef == null)
            return;

        pawn.mindState.mentalStateHandler.TryStartMentalState(
            stateDef,
            forced: false,
            forceWake: false,
            causedByMood: false);
    }
}
