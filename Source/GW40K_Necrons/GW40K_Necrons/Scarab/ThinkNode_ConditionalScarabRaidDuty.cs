using Verse;
using Verse.AI;

namespace GW40K_Necrons;

public class ThinkNode_ConditionalSapperDuty : ThinkNode_Conditional
{
    protected override bool Satisfied(Pawn pawn) =>
        ScarabRaidDutyUtility.IsHostileRaider(pawn)
        && ScarabRaidDutyUtility.IsOnSapperDuty(pawn);
}

public class ThinkNode_ConditionalBreachingDuty : ThinkNode_Conditional
{
    protected override bool Satisfied(Pawn pawn) =>
        ScarabRaidDutyUtility.IsHostileRaider(pawn)
        && ScarabRaidDutyUtility.IsOnBreachingDuty(pawn);
}
