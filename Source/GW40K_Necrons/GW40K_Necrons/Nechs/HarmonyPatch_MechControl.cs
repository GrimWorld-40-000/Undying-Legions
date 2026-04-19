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
        if (mech == null || pawn == null)
            return;

        bool isNecronMech = mech.def.GetModExtension<NecronMechExtension>() != null;
        bool hasCommandProtocol = pawn.health?.hediffSet?.GetFirstHediffOfDef(
            HediffDef.Named("GW40K_CommandProtocolImplant")) != null;
        bool hasVanillaMechlink = pawn.health?.hediffSet?.GetFirstHediffOfDef(
            HediffDef.Named("MechlinkImplant")) != null;

        if (__result.Accepted)
        {
            if (isNecronMech && !hasCommandProtocol)
            {
                __result = "Necron constructs obey only those who bear the command protocol.";
                return;
            }

            if (!isNecronMech && hasCommandProtocol && !hasVanillaMechlink)
            {
                __result = "The command protocol governs only Necron constructs.";
                return;
            }

            if (isNecronMech && hasCommandProtocol)
            {
                HediffComp_NecronCommandTracker tracker = HediffComp_NecronCommandTracker.GetTracker(pawn);
                if (tracker != null && !tracker.controlledMechs.Contains(mech) && !tracker.HasBandwidthFor(mech))
                    __result = "GW40K_CommandBandwidthFull".Translate();
                else if (tracker != null && !tracker.IsWithinControlRange(mech))
                    __result = "GW40K_CommandOutOfRange".Translate(tracker.ControlRange.ToString("0.#"));
            }

            return;
        }

        // Vanilla rejected (typically no Mechlink). Nechinator / Command Protocol can still take unassigned Necron constructs.
        if (!ModsConfig.BiotechActive)
            return;
        if (!isNecronMech || !hasCommandProtocol)
            return;
        if (HediffComp_NecronCommandTracker.GetCommanderOf(mech) != null)
            return;
        if (mech.Faction != pawn.Faction)
            return;

        HediffComp_NecronCommandTracker tr = HediffComp_NecronCommandTracker.GetTracker(pawn);
        if (tr == null)
            return;
        if (tr.controlledMechs.Contains(mech))
        {
            __result = AcceptanceReport.WasAccepted;
            return;
        }

        if (tr.HasBandwidthFor(mech))
        {
            if (tr.IsWithinControlRange(mech))
                __result = AcceptanceReport.WasAccepted;
            else
                __result = "GW40K_CommandOutOfRange".Translate(tr.ControlRange.ToString("0.#"));
        }
        else
            __result = "GW40K_CommandBandwidthFull".Translate();
    }
}
