using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof(Designator_Uninstall), nameof(Designator_Uninstall.CanDesignateThing))]
public static class HarmonyPatch_StasisCryptUninstall
{
    public static void Postfix(Thing t, ref AcceptanceReport __result)
    {
        if (t is not NecronCasket casket || casket.ContainedThing == null)
            return;
        __result = "GW40K_StasisCryptCannotUninstallOccupied".Translate();
    }
}
