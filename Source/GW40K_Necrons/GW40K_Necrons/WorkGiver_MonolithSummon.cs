using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// WorkGiver for the Monolith bench, restricted to pawns bearing the Command Protocol.
/// Extends WorkGiver_DoBill so all normal bill-handling logic applies — only the
/// eligibility check is overridden to gate on the implant.
/// </summary>
public class WorkGiver_MonolithSummon : WorkGiver_DoBill
{
    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        if (base.ShouldSkip(pawn, forced)) return true;
        return pawn.health?.hediffSet?.GetFirstHediffOfDef(
            HediffDef.Named("GW40K_CommandProtocolImplant")) == null;
    }
}
