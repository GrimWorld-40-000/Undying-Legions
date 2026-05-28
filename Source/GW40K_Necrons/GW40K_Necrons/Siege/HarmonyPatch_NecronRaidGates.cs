using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Gates all natural Necron raid incidents behind the Electricity research project.
///
/// Necrons sense the electromagnetic signature of a powered colony before committing
/// an attack force — without electricity the colony reads as a pre-industrial settlement
/// beneath their notice.
///
/// Patches <see cref="IncidentWorker.CanFireNow"/> — the outermost public gate that every
/// incident worker calls. Confirmed signature in this build: <c>CanFireNow(IncidentParms)</c>
/// — no forced flag. We scope to raid incidents via <c>__instance is IncidentWorker_Raid</c>
/// and to the Necron faction via defName.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker), "CanFireNow")]
public static class HarmonyPatch_NecronRaidElectricityGate
{
    private static ResearchProjectDef _electricity;
    private static ResearchProjectDef Electricity =>
        _electricity ??= DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Electricity");

    [HarmonyPostfix]
    public static void Postfix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        if (!__result) return;

        // Only gate IncidentWorker_Raid subclasses (covers RaidEnemy, RaidFriendly, etc.).
        if (!(__instance is IncidentWorker_Raid)) return;

        // Only gate Necron faction raids; pass all others through unchanged.
        if (parms?.faction?.def?.defName != "UD_NecronFaction") return;

        ResearchProjectDef elec = Electricity;
        if (elec != null && !elec.IsFinished)
            __result = false;
    }
}
