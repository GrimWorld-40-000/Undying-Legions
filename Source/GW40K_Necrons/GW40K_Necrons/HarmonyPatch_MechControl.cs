using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Separates Necron mech control from vanilla mechanitor control.
/// - Necron mechs (NecronMechExtension) can only be controlled by Command Protocol bearers.
/// - Vanilla mechs cannot be controlled by Command Protocol bearers (unless they also have a vanilla Mechlink).
/// </summary>
[HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.CanControlMech))]
public static class HarmonyPatch_MechControl
{
    [HarmonyPostfix]
    public static void Postfix(Pawn pawn, Pawn mech, ref AcceptanceReport __result)
    {
        // Only intervene if the base game already allowed it — don't override an already-failing check
        if (!__result.Accepted) return;

        bool isNecronMech = mech.def.GetModExtension<NecronMechExtension>() != null;
        bool hasCommandProtocol = pawn.health?.hediffSet?.GetFirstHediffOfDef(
            HediffDef.Named("GW40K_CommandProtocolImplant")) != null;
        bool hasVanillaMechlink = pawn.health?.hediffSet?.GetFirstHediffOfDef(
            HediffDef.Named("MechlinkImplant")) != null;

        if (isNecronMech && !hasCommandProtocol)
        {
            __result = "Necron constructs obey only those who bear the command protocol.";
            return;
        }

        if (!isNecronMech && hasCommandProtocol && !hasVanillaMechlink)
        {
            __result = "The command protocol governs only Necron constructs.";
        }
    }
}
