using RimWorld;
using UnityEngine;
using Verse;

namespace GW40K_Necrons;

public class ScarabSelfDestruct : CompAbilityEffect
{
    public new ScarabSelfDestructProperties Props => (ScarabSelfDestructProperties)props;

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        Pawn pawn = parent.pawn;
        if (pawn.Dead || pawn.Map == null)
            return;

        // Count how many scarab units are still alive (not missing)
        int alive = 0;
        foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
        {
            if (part.def.defName == "GW40K_ScarabUnit")
                alive++;
        }

        if (alive <= 0)
            return;

        float scale = alive / 4f;
        float damage = Props.baseDamage * scale;
        float radius = Props.baseRadius * scale;

        GenExplosion.DoExplosion(
            center: pawn.Position,
            map: pawn.Map,
            radius: radius,
            damType: DamageDefOf.Bomb,
            instigator: pawn,
            damAmount: Mathf.RoundToInt(damage)
        );

        if (!pawn.Dead)
            pawn.Kill(new DamageInfo(DamageDefOf.Bomb, 9999f, instigator: pawn));
    }
}
