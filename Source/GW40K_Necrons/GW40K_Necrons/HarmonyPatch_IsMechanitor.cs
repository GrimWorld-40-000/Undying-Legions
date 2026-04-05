using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Makes MechanitorUtility.IsMechanitor return true for pawns bearing the
/// Command Protocol implant, so they receive the bandwidth bar, control groups,
/// and a valid Pawn_MechanitorTracker (actor.mechanitor != null).
/// </summary>
[HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.IsMechanitor))]
public static class HarmonyPatch_IsMechanitor
{
    [HarmonyPostfix]
    public static void Postfix(Pawn p, ref bool __result)
    {
        if (__result) return;
        __result = p?.health?.hediffSet?.GetFirstHediffOfDef(
            HediffDef.Named("GW40K_CommandProtocolImplant")) != null;
    }
}
