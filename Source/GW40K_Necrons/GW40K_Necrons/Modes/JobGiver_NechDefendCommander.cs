using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Escort mode combat — fights enemies within the commander's control range.
/// Mirrors <c>JobGiver_AIDefendOverseer</c>; uses our Necron command tracker.
/// </summary>
public class JobGiver_NechDefendCommander : JobGiver_AIDefendPawn
{
    protected override Pawn GetDefendee(Pawn pawn) =>
        HediffComp_NecronCommandTracker.GetCommanderOf(pawn);

    protected override float GetFlagRadius(Pawn pawn)
    {
        Pawn commander = HediffComp_NecronCommandTracker.GetCommanderOf(pawn);
        if (commander == null) return 40f;
        HediffComp_NecronCommandTracker tracker = HediffComp_NecronCommandTracker.GetTracker(commander);
        return tracker?.ControlRange ?? 40f;
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
        // Guard: base class logs an error if defendee is null.
        if (HediffComp_NecronCommandTracker.GetCommanderOf(pawn) == null) return null;
        return base.TryGiveJob(pawn);
    }
}
