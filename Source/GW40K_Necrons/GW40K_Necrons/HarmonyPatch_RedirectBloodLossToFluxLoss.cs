using HarmonyLib;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Redirects <see cref="HediffDefOf.BloodLoss"/> to GW40K_CoreFluxLoss for any pawn whose
/// race uses Necron core flux as its blood filth (bloodDef == GW40K_Filth_NecronBlood).
///
/// Both accumulation and recovery run through <see cref="HealthUtility.AdjustSeverity"/>,
/// so a single prefix is sufficient: positive calls build CoreFluxLoss severity, negative
/// calls reduce it.  BloodLoss is never created for these pawns, so vanilla alerts and
/// UI strings keyed to BloodLoss are silenced automatically.
/// </summary>
[HarmonyPatch(typeof(HealthUtility), nameof(HealthUtility.AdjustSeverity))]
public static class HarmonyPatch_RedirectBloodLossToFluxLoss
{
    private static HediffDef _coreFluxLoss;
    private static ThingDef  _necronBloodFilth;

    private static HediffDef CoreFluxLoss =>
        _coreFluxLoss ??= DefDatabase<HediffDef>.GetNamedSilentFail("GW40K_CoreFluxLoss");

    private static ThingDef NecronBloodFilth =>
        _necronBloodFilth ??= DefDatabase<ThingDef>.GetNamedSilentFail("GW40K_Filth_NecronBlood");

    [HarmonyPrefix]
    public static void Prefix(Pawn pawn, ref HediffDef hdDef, float sevOffset)
    {
        if (hdDef != HediffDefOf.BloodLoss) return;
        if (CoreFluxLoss == null || NecronBloodFilth == null) return;
        if (pawn?.RaceProps?.BloodDef != NecronBloodFilth) return;

        hdDef = CoreFluxLoss;
    }
}
