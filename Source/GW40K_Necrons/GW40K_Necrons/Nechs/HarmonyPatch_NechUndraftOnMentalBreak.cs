using HarmonyLib;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof(MentalState), nameof(MentalState.PostStart))]
public static class HarmonyPatch_NechUndraftOnMentalBreak
{
    [HarmonyPostfix]
    public static void Postfix(MentalState __instance)
    {
        Pawn pawn = __instance.pawn;
        if (pawn?.def.GetModExtension<NecronMechExtension>() == null)
            return;
        if (pawn.drafter == null || !pawn.Drafted)
            return;

        pawn.drafter.Drafted = false;
        pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced, false);
    }
}
