using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Escort mode follow — keeps the Necron within the commander's control range.
/// Mirrors <c>JobGiver_AIFollowOverseer</c>; uses our Necron command tracker instead
/// of the vanilla overseer relation.
/// </summary>
public class JobGiver_NechEscortCommander : JobGiver_AIFollowPawn
{
    protected override int FollowJobExpireInterval => 200;

    protected override Pawn GetFollowee(Pawn pawn) =>
        HediffComp_NecronCommandTracker.GetCommanderOf(pawn);

    protected override float GetRadius(Pawn pawn)
    {
        Pawn commander = HediffComp_NecronCommandTracker.GetCommanderOf(pawn);
        if (commander == null) return 40f;
        HediffComp_NecronCommandTracker tracker = HediffComp_NecronCommandTracker.GetTracker(commander);
        return tracker?.ControlRange ?? 40f;
    }

    protected override Job TryGiveJob(Pawn pawn)
    {
        // Guard: base class logs an error if followee is null.
        if (HediffComp_NecronCommandTracker.GetCommanderOf(pawn) == null) return null;
        return base.TryGiveJob(pawn);
    }
}
