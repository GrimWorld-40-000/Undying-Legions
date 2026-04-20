using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Every 4 hours, Necron pawns with necrodermis above 20% can reconstruct one missing body part.
/// Half the body part's necrodermis cost is consumed; the part returns at 10% HP.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.TickRare))]
public static class HarmonyPatch_NecronReconstruction
{
    // 4 hours = 10,000 ticks; IsHashIntervalTick staggers this across pawns by thingIDNumber.
    private const int ReconstructionInterval = 10000;
    private const float NecrodermisMinThreshold = 0.20f;
    // Necrodermis cost per body part HP (0-1 scale). A 30 HP arm costs 0.30 total, 0.15 to start.
    private const float NecrodermisPerHitPoint = 0.01f;

    [HarmonyPostfix]
    public static void Postfix(Pawn __instance)
    {
        // Fire once per 40 TickRare calls (40 × 250 = 10 000 ticks = 4 h), staggered by pawn ID.
        if ((Find.TickManager.TicksGame / 250 + __instance.thingIDNumber) % 40 != 0) return;
        if (!__instance.Spawned || __instance.Dead) return;
        if (!NechEnergyUtility.IsNecronPawn(__instance)) return;

        TryReconstruct(__instance);
    }

    private static void TryReconstruct(Pawn pawn)
    {
        if (pawn.health.summaryHealth.SummaryHealthPercent >= 1f) return;

        if (NecronDefOfs.GW_UD_Necrodermis == null) return;
        Need necrodermis = pawn.needs?.TryGetNeed(NecronDefOfs.GW_UD_Necrodermis);
        if (necrodermis == null) return;
        if (necrodermis.CurLevelPercentage < NecrodermisMinThreshold) return;

        Hediff_MissingPart missing = pawn.health.hediffSet.hediffs
            .OfType<Hediff_MissingPart>()
            .Where(h => !h.IsFresh)
            .RandomElementWithFallback(null);

        if (missing == null) return;

        float halfCost = missing.Part.def.hitPoints * NecrodermisPerHitPoint * 0.5f;
        if (necrodermis.CurLevel < halfCost) return;

        necrodermis.CurLevel -= halfCost;

        BodyPartRecord part = missing.Part;
        pawn.health.RestorePart(part);

        // Wound the restored part to ~10% HP so it must finish healing normally.
        float maxHp = part.def.GetMaxHealth(pawn);
        HediffDef woundDef = DefDatabase<HediffDef>.GetNamed("Scratch", errorOnFail: false)
                          ?? DefDatabase<HediffDef>.GetNamed("Stab",    errorOnFail: false);
        if (woundDef != null)
        {
            var wound = (Hediff_Injury)HediffMaker.MakeHediff(woundDef, pawn, part);
            wound.Severity = maxHp * 0.9f;
            pawn.health.AddHediff(wound, part, null);
        }

        if (PawnUtility.ShouldSendNotificationAbout(pawn))
        {
            Messages.Message(
                "GW40K_NecronReconstructed".Translate(pawn.Named("PAWN")),
                pawn,
                MessageTypeDefOf.PositiveEvent);
        }
    }
}
