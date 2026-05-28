using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>Attack a Necron corpse until destroyed (prevent resurrection). Corpses are not valid <see cref="JobDefOf.AttackStatic"/> targets.</summary>
public class JobDriver_DestroyNecronCorpse : JobDriver
{
    private Corpse TargetCorpse => job.targetA.Thing as Corpse;

    /// <summary>
    /// True when the pawn's primary weapon is ranged AND should be used against the corpse.
    /// Excluded cases that fall back to melee:
    /// <list type="bullet">
    ///   <item>Tesla Carbine — arc chains are wasted on a single static target.</item>
    ///   <item>Gauss ranged weapon with energy below 50% — conserve charge for live targets.</item>
    /// </list>
    /// </summary>
    private bool ShouldUseRanged
    {
        get
        {
            ThingWithComps primary = pawn.equipment?.Primary;
            if (primary == null) return false;
            if (primary.def?.defName == "GW40k_Necron_TeslaCarbine") return false;

            // Gauss ranged weapons: prefer melee when the pawn is below half charge.
            ModExtension_GaussWeapon gaussExt = GaussWeaponUtil.RangedExt(primary);
            if (gaussExt != null && GaussWeaponUtil.GaussEnergy(pawn) < 0.5f) return false;

            Verb v = primary.GetComp<CompEquippable>()?.PrimaryVerb;
            return v != null && !v.verbProps.IsMeleeAttack;
        }
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        // maxNumReservers=10 with stackCount=1: up to 10 pawns can pile on the same corpse.
        // stackCount must be a specific count (not -1/All) when maxNumReservers > 1,
        // otherwise RimWorld ignores the reservation entirely.
        return TargetCorpse != null && pawn.Reserve(TargetCorpse, job, 10, 1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        // FailOn fires next tick after the corpse is destroyed — ends the job cleanly.
        this.FailOnDespawnedOrNull(TargetIndex.A);
        this.FailOn(() => TargetCorpse == null || !TargetCorpse.Spawned);

        // Melee pawns (and Tesla Carbine holders) advance to touch range up front;
        // other ranged pawns handle movement in tickAction.
        if (!ShouldUseRanged)
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        Toil attack = ToilMaker.MakeToil("AttackNecronCorpse");
        attack.defaultCompleteMode = ToilCompleteMode.Never;
        attack.tickAction = () =>
        {
            Corpse c = TargetCorpse;
            if (c == null || !c.Spawned || c.Destroyed)
                return; // FailOn handles cleanup next tick

            pawn.rotationTracker.FaceCell(c.Position);

            if (pawn.stances.FullBodyBusy)
                return;

            // Re-evaluate each tick in case weapon changes mid-job.
            bool useRanged = ShouldUseRanged;
            Verb primaryVerb = useRanged
                ? pawn.equipment?.Primary?.GetComp<CompEquippable>()?.PrimaryVerb
                : null;

            // Eligible ranged weapon and in range: stop moving and fire.
            if (useRanged && primaryVerb != null && primaryVerb.CanHitTarget(c))
            {
                pawn.pather.StopDead();
                primaryVerb.TryStartCastOn(c);
                return;
            }

            // Ranged out of range, melee pawn, or Tesla Carbine holder: walk to touch range.
            // LengthHorizontalSquared > 2 means not adjacent (cardinal = 1, diagonal = 2).
            if ((pawn.Position - c.Position).LengthHorizontalSquared > 2)
            {
                if (!pawn.pather.Moving)
                    pawn.pather.StartPath(c, PathEndMode.Touch);
                return;
            }

            // At touch range: stop and melee.
            pawn.pather.StopDead();
            Verb verb = pawn.TryGetAttackVerb(c);
            if (verb != null)
                verb.TryStartCastOn(c);
            else
                c.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 25f, 0f, -1f, pawn));
        };
        yield return attack;
    }
}
