using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

internal static class NechInspectStringUtility
{
    public static bool IsNechProperlyCommanded(Pawn nech)
    {
        if (nech == null)
            return false;
        Pawn commander = nech.GetOverseer();
        return commander != null
            && HediffComp_NecronCommandTracker.GetTracker(commander)?.controlledMechs?.Contains(nech) == true;
    }
}
