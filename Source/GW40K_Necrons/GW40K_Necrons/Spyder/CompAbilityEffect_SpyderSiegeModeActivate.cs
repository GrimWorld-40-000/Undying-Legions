using RimWorld;
using Verse;

namespace GW40K_Necrons;

public class CompProperties_SpyderSiegeModeActivate : CompProperties_AbilityEffect
{
    public CompProperties_SpyderSiegeModeActivate() =>
        compClass = typeof(CompAbilityEffect_SpyderSiegeModeActivate);
}

/// <summary>
/// Applies GW_UL_SpyderSiegeModeManual (3-hour timed siege mode) to the casting Spyder.
/// Blocked if the Spyder already has siege mode active (manual or siege-lord granted).
/// </summary>
public class CompAbilityEffect_SpyderSiegeModeActivate : CompAbilityEffect
{
    private const string ManualHediffName = "GW_UL_SpyderSiegeModeManual";
    private const string SiegeHediffName  = "GW_UL_SpyderSiegeMode";

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        Pawn pawn = parent.pawn;
        if (pawn == null) return;

        HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(ManualHediffName);
        if (def == null) return;

        if (!pawn.health.hediffSet.HasHediff(def))
            pawn.health.AddHediff(def);
        // Cooldown is reset via HediffComp_SpyderSiegeMode.CompPostPostAdd when the hediff is added.
        // If the hediff was already present, reset explicitly so the cannon is ready immediately.
        else
            pawn.TryGetComp<Comp_SpyderSiegeCannon>()?.ResetCooldown();
    }

    public override bool GizmoDisabled(out string reason)
    {
        Pawn pawn = parent.pawn;
        if (pawn != null)
        {
            // Already in siege mode (manual).
            HediffDef manual = DefDatabase<HediffDef>.GetNamedSilentFail(ManualHediffName);
            if (manual != null && pawn.health.hediffSet.HasHediff(manual))
            {
                reason = "GW40K_SpyderAlreadyInSiegeMode".Translate();
                return true;
            }

            // Already in siege mode from a siege lord.
            HediffDef siege = DefDatabase<HediffDef>.GetNamedSilentFail(SiegeHediffName);
            if (siege != null && pawn.health.hediffSet.HasHediff(siege))
            {
                reason = "GW40K_SpyderAlreadyInSiegeMode".Translate();
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }
}
