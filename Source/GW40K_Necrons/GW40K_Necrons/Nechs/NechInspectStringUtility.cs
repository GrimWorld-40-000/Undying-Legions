using Verse;

#nullable disable
namespace GW40K_Necrons;

internal static class NechInspectStringUtility
{
    public static bool IsNechProperlyCommanded(Pawn nech)
    {
        return HediffComp_NecronCommandTracker.GetCommanderOf(nech) != null;
    }
}
