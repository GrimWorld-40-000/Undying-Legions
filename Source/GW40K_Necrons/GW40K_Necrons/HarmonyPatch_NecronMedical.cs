using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

// ── Schedule: all slots = Anything (no forced sleep periods) ─────────────────

/// <summary>
/// On spawn, sets all 24 timetable slots to Anything for Necron pawns so they
/// never enter a forced-sleep time block. Necrons don't need rest; sleeping is
/// handled exclusively by the stasis crypt think-tree.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
public static class HarmonyPatch_NecronScheduleAnything
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance)
    {
        if (!NechEnergyUtility.IsNecronPawn(__instance)) return;
        if (__instance.timetable == null) return;
        for (int h = 0; h < 24; h++)
            __instance.timetable.SetAssignment(h, TimeAssignmentDefOf.Anything);
    }
}

// ── Bed rest: Necrons must not seek medical beds ──────────────────────────────

/// <summary>
/// Blocks Necron pawns from autonomously seeking medical beds for rest/healing.
/// Recovery is handled by the stasis crypt and Canoptek repair, not bed rest.
/// </summary>
[HarmonyPatch(typeof(JobGiver_PatientGoToBed), "TryGiveJob")]
public static class HarmonyPatch_NecronNoBedRest
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn pawn, ref Job __result)
    {
        if (!NechEnergyUtility.IsNecronPawn(pawn)) return true;
        __result = null;
        return false;
    }
}

// ── Doctors: cannot tend Necron pawns ────────────────────────────────────────

/// <summary>
/// Prevents colonist doctors from tending to Necron pawns. Only Canoptek repair
/// (JobGiver_CanoptekRepair / JobDriver_CanoptekRepair) can heal Necrons.
/// </summary>
[HarmonyPatch(typeof(WorkGiver_Tend), nameof(WorkGiver_Tend.HasJobOnThing))]
public static class HarmonyPatch_NecronNoDocTreatment
{
    [HarmonyPrefix]
    public static bool Prefix(Thing t, ref bool __result)
    {
        if (t is Pawn patient && NechEnergyUtility.IsNecronPawn(patient))
        {
            __result = false;
            return false;
        }
        return true;
    }
}

// ── Beds: Necrons may only use NecronCasket (stasis crypt) ───────────────────

/// <summary>
/// Nulls any Building_Bed result for Necron pawns from FindBedFor.
/// NecronCasket extends Building_CryptosleepCasket (not Building_Bed) so it is
/// never returned here anyway. Blocking the call entirely prevents Necrons from
/// being assigned regular beds; the stasis crypt path runs through NecroCasketUtility.
/// </summary>
[HarmonyPatch(typeof(RestUtility), nameof(RestUtility.FindBedFor),
    typeof(Pawn), typeof(Pawn), typeof(bool), typeof(bool), typeof(GuestStatus?))]
public static class HarmonyPatch_NecronOnlyStasisBed
{
    [HarmonyPostfix]
    public static void Postfix(Pawn sleeper, ref Building_Bed __result)
    {
        if (!NechEnergyUtility.IsNecronPawn(sleeper)) return;
        __result = null; // Necrons never use regular beds
    }
}
