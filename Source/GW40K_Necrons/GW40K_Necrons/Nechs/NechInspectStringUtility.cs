using Verse;

#nullable disable
namespace GW40K_Necrons;

internal static class NechInspectStringUtility
{
    public static bool IsNechProperlyCommanded(Pawn nech)
    {
        if (nech == null)
            return false;
        if (HediffComp_NecronCommandTracker.GetCommanderOf(nech) != null)
            return true;
        return HediffComp_ControlNodeTracker.GetControllerOfConstruct(nech) != null;
    }
}
