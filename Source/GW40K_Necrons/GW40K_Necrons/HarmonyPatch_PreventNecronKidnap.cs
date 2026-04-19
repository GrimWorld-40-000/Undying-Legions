using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Prevents Necrons from being kidnapped. Two job givers cover the two scenarios:
/// - JobGiver_Kidnap: raider kidnap raids targeting colonists
/// - JobGiver_TakeWoundedGuest: allied/neutral factions rescuing downed guests
/// </summary>
[HarmonyPatch(typeof(JobGiver_Kidnap), "TryGiveJob")]
public static class HarmonyPatch_PreventNecronKidnap
{
    [HarmonyPostfix]
    public static void Postfix(ref Job __result)
    {
        if (__result == null)
            return;

        Pawn target = __result.targetA.Pawn;
        if (target?.def.GetModExtension<NonOrganicPawn>() == null)
            return;

        __result = null;
    }
}

[HarmonyPatch(typeof(JobGiver_TakeWoundedGuest), "TryGiveJob")]
public static class HarmonyPatch_PreventNecronGuestRescue
{
    [HarmonyPostfix]
    public static void Postfix(ref Job __result)
    {
        if (__result == null)
            return;

        Pawn target = __result.targetA.Pawn;
        if (target?.def.GetModExtension<NonOrganicPawn>() == null)
            return;

        __result = null;
    }
}
