using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Integrated long-range siege cannon for the Canoptek Spyder.
/// Only available while siege mode is active.
///
/// AI blocking is handled upstream — NechIntegratedAttackUtility.TryGetPreferredRangedVerb
/// unconditionally skips Verb_SpyderSiegeCannon, so no AI path ever selects it.
/// Available() therefore only needs to check siege mode and does not need to special-case
/// player vs enemy factions.
///
/// CanHitTarget is overridden to also gate on Available(). The vanilla AttackTargetFinder
/// uses CanHitTarget (not Available) when scanning for targets, so without this override
/// the siege cannon's range-500 CanHitTarget would cause the Spyder to lock onto enemies
/// across the entire map whenever it is undrafted — even with siege mode inactive.
/// When Available() is true (siege mode on), base.CanHitTarget is used unchanged so the
/// player can target normally.
/// </summary>
public class Verb_SpyderSiegeCannon : Verb_Shoot
{
    public override bool Available()
    {
        if (!base.Available()) return false;

        Pawn pawn = CasterPawn;
        if (pawn == null) return true;

        // Only available during siege mode — switches off when the Spyder leaves siege duty.
        if (!HediffComp_SpyderSiegeMode.IsSiegeMode(pawn)) return false;

        var comp = pawn.TryGetComp<Comp_SpyderSiegeCannon>();
        return comp == null || comp.IsReady;
    }

    /// <summary>
    /// Prevents the vanilla AttackTargetFinder from selecting far-away targets when siege
    /// mode is inactive. Available() is false then, so we report no target as hittable.
    /// </summary>
    public override bool CanHitTarget(LocalTargetInfo targ)
    {
        if (!Available()) return false;
        return base.CanHitTarget(targ);
    }
}
