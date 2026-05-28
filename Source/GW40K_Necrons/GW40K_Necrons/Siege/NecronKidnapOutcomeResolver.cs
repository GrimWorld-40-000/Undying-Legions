using RimWorld;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Possible fates for a pawn taken by the Necrons during a siege withdrawal.
/// Each outcome is a distinct path in a branching narrative.
/// </summary>
public enum NecronKidnapOutcome
{
    /// <summary>No trace found — feared dead but outcome unknown.</summary>
    Death,
    /// <summary>The captive escaped before the process could begin.</summary>
    EscapedSafely,
    /// <summary>Early-stage necrodermis colonization — returned alive but changed.</summary>
    Enslaved,
    /// <summary>Full Vekh conversion — living metal runs deep, will is suppressed.</summary>
    Vekh,
    /// <summary>Complete biotransference — the captive is biologically gone.</summary>
    Biotransferred,
}

/// <summary>
/// Rolls and schedules kidnap outcomes; applies them after a 3–7 day delay
/// so the revelation feels like news arriving, not instant knowledge.
///
/// <see cref="Resolve"/> is called immediately on lord cleanup — it rolls the
/// outcome and hands it to <see cref="GameComponent_PendingKidnapOutcomes"/>.
/// <see cref="ApplyOutcome"/> is called by that component 3–7 days later.
/// </summary>
public static class NecronKidnapOutcomeResolver
{
    // Weighted thresholds (cumulative). Adjust freely.
    private const float ThreshDeath    = 0.15f;
    private const float ThreshEscaped  = 0.35f; // +0.20
    private const float ThreshEnslaved = 0.70f; // +0.35
    private const float ThreshVekh     = 0.95f; // +0.25
    // Biotransferred fills the remaining 0.05

    // ── Public entry points ───────────────────────────────────────────────────

    /// <summary>
    /// Rolls the outcome and schedules it for deferred reveal (3–7 days).
    /// The captive's mechanical state is not touched here — they remain in
    /// WorldPawns until <see cref="ApplyOutcome"/> fires.
    /// </summary>
    public static void Resolve(Pawn captive, Faction necronFaction, Map _map)
    {
        if (captive == null || captive.Destroyed) return;
        if (!PawnUtility.ShouldSendNotificationAbout(captive)) return;

        NecronKidnapOutcome outcome = RollOutcome();
        int factionId = necronFaction?.loadID ?? -1;

        GameComponent_PendingKidnapOutcomes.Current
            ?.Schedule(captive.thingIDNumber, outcome, factionId);
    }

    /// <summary>
    /// Applies the pre-rolled outcome. Called by
    /// <see cref="GameComponent_PendingKidnapOutcomes"/> after the delay.
    /// The map parameter is the player's home map at fire time.
    /// </summary>
    public static void ApplyOutcome(NecronKidnapOutcome outcome, Pawn captive, Faction necronFaction, Map map)
    {
        if (captive == null || captive.Destroyed) return;

        switch (outcome)
        {
            case NecronKidnapOutcome.Death:
                Outcome_Death(captive);
                break;
            case NecronKidnapOutcome.EscapedSafely:
                Outcome_EscapedSafely(captive, map);
                break;
            case NecronKidnapOutcome.Enslaved:
                Outcome_Enslaved(captive, map);
                break;
            case NecronKidnapOutcome.Vekh:
                Outcome_Vekh(captive, map);
                break;
            case NecronKidnapOutcome.Biotransferred:
                Outcome_Biotransferred(captive);
                break;
        }
    }

    // ── Outcome roll ──────────────────────────────────────────────────────────

    private static NecronKidnapOutcome RollOutcome()
    {
        float r = Rand.Value;
        if (r < ThreshDeath)    return NecronKidnapOutcome.Death;
        if (r < ThreshEscaped)  return NecronKidnapOutcome.EscapedSafely;
        if (r < ThreshEnslaved) return NecronKidnapOutcome.Enslaved;
        if (r < ThreshVekh)     return NecronKidnapOutcome.Vekh;
        return NecronKidnapOutcome.Biotransferred;
    }

    // ── Outcome implementations ───────────────────────────────────────────────

    private static void Outcome_Death(Pawn captive)
    {
        if (!captive.Dead)
            captive.Kill(null);

        Find.LetterStack.ReceiveLetter(
            "GW40K_KidnapOutcomeDeath_Label".Translate(captive.Named("PAWN")),
            "GW40K_KidnapOutcomeDeath_Desc".Translate(captive.Named("PAWN")),
            LetterDefOf.NegativeEvent, captive);
    }

    private static void Outcome_EscapedSafely(Pawn captive, Map map)
    {
        ReturnCaptiveToMap(captive, map);

        Find.LetterStack.ReceiveLetter(
            "GW40K_KidnapOutcomeEscaped_Label".Translate(captive.Named("PAWN")),
            "GW40K_KidnapOutcomeEscaped_Desc".Translate(captive.Named("PAWN")),
            LetterDefOf.PositiveEvent, captive);
    }

    private static void Outcome_Enslaved(Pawn captive, Map map)
    {
        ApplyNecrodermisGrowth(captive, Rand.Range(0.1f, 0.3f));
        ApplyVekhPainblocker(captive);

        ReturnCaptiveToMap(captive, map);

        Find.LetterStack.ReceiveLetter(
            "GW40K_KidnapOutcomeEnslaved_Label".Translate(captive.Named("PAWN")),
            "GW40K_KidnapOutcomeEnslaved_Desc".Translate(captive.Named("PAWN")),
            LetterDefOf.NeutralEvent, captive);
    }

    private static void Outcome_Vekh(Pawn captive, Map map)
    {
        ApplyNecrodermisGrowth(captive, Rand.Range(0.5f, 0.75f));
        ApplyVekhPainblocker(captive);

        // TODO: apply GW_UL_VekhThrall trait and work disables matching GW_UL_VekhHost_Ad.
        // TODO: adjust pawnkind or backstory to reflect the conversion.

        ReturnCaptiveToMap(captive, map);

        Find.LetterStack.ReceiveLetter(
            "GW40K_KidnapOutcomeVekh_Label".Translate(captive.Named("PAWN")),
            "GW40K_KidnapOutcomeVekh_Desc".Translate(captive.Named("PAWN")),
            LetterDefOf.NeutralEvent, captive);
    }

    private static void Outcome_Biotransferred(Pawn captive)
    {
        if (!captive.Dead)
            captive.Kill(null);

        // TODO: optionally spawn as an enemy Necron construct in a future raid.

        Find.LetterStack.ReceiveLetter(
            "GW40K_KidnapOutcomeBiotransferred_Label".Translate(captive.Named("PAWN")),
            "GW40K_KidnapOutcomeBiotransferred_Desc".Translate(captive.Named("PAWN")),
            LetterDefOf.Death, captive);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void ApplyNecrodermisGrowth(Pawn pawn, float severity)
    {
        HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("Necron_NecrodermisGrowth");
        if (def == null) return;
        Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
        if (existing != null)
            existing.Severity = System.Math.Max(existing.Severity, severity);
        else
        {
            Hediff h = HediffMaker.MakeHediff(def, pawn);
            h.Severity = severity;
            pawn.health.AddHediff(h);
        }
    }

    private static void ApplyVekhPainblocker(Pawn pawn)
    {
        HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("GW_UL_VekhThrallPainblocker");
        if (def != null && !pawn.health.hediffSet.HasHediff(def))
            pawn.health.AddHediff(def);
    }

    private static void ReturnCaptiveToMap(Pawn captive, Map map)
    {
        if (captive.Dead || captive.Destroyed || map == null) return;
        if (captive.Spawned) return;

        if (!CellFinder.TryFindRandomEdgeCellWith(
                c => c.Standable(map) && !c.Fogged(map) &&
                     map.reachability.CanReachMapEdge(c, TraverseParms.For(TraverseMode.PassDoors)),
                map, CellFinder.EdgeRoadChance_Neutral, out IntVec3 cell))
            cell = CellFinder.RandomEdgeCell(Rot4.Random, map);

        GenSpawn.Spawn(captive, cell, map);
    }
}
