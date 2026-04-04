using RimWorld;
using Verse;

#nullable disable
namespace NecronComp;

public class IncidentWorker_NecrodemisInfection : IncidentWorker
{
    protected override bool CanFireNowSub(IncidentParms parms) => false;
    protected override bool TryExecuteWorker(IncidentParms parms) => false;
}
