using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Vanilla <see cref="Pawn_HealthTracker.HealthTickInterval"/> can enqueue <c>MessageFullyHealed</c> once per
/// 600-tick natural-heal pulse when there are no tended injuries awaiting the player — even if
/// <see cref="HediffSet.HasNaturallyHealingInjury"/> is still true. Necrons with <c>WoundHealing_Fast</c>
/// therefore spam positives every heal cycle during slow injury cleanup (Immortal / Cryptothrall, etc.).
/// </summary>
[HarmonyPatch(typeof(Messages))]
[HarmonyPatch(nameof(Messages.Message))]
[HarmonyPatch(new[] { typeof(string), typeof(LookTargets), typeof(MessageTypeDef), typeof(bool) })]
public static class HarmonyPatch_SuppressNecronFullyHealedSpam
{
    [HarmonyPrefix]
    public static bool Prefix(string text, LookTargets lookTargets)
    {
        if (!lookTargets.IsValid)
            return true;

        Pawn pawn = lookTargets.PrimaryTarget.Pawn;
        if (pawn == null || !pawn.RaceProps.Humanlike || !NechEnergyUtility.IsNecronPawn(pawn))
            return true;

        if (!IsFullyHealedMessageText(pawn, text))
            return true;

        if (pawn.health?.hediffSet == null)
            return true;

        if (pawn.health.hediffSet.HasNaturallyHealingInjury())
            return false;

        if (pawn.health.summaryHealth != null && pawn.health.summaryHealth.SummaryHealthPercent < 1f - 1e-3f)
            return false;

        return true;
    }

    private static bool IsFullyHealedMessageText(Pawn pawn, string text)
    {
        string a = "MessageFullyHealed".Translate(pawn.LabelCap, pawn).Resolve();
        if (text == a)
            return true;
        string b = "MessageFullyHealed".Translate(pawn.LabelShortCap.CapitalizeFirst(), pawn).Resolve();
        return text == b;
    }
}
