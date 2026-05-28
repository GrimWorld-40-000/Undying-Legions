using RimWorld;
using UnityEngine;
using Verse;

namespace GW40K_Necrons;

internal static class ScarabSelfDestructUtility
{
    private const string ScarabRaceDefName = "GW40K_ScarabSwarm";

    internal static bool IsScarabSwarm(Pawn pawn) =>
        pawn?.def?.defName == ScarabRaceDefName;

    internal static int AliveUnitCount(Pawn pawn)
    {
        int alive = 0;
        foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
        {
            if (part.def.defName == "GW40K_ScarabUnit")
                alive++;
        }
        return alive;
    }

    internal static bool HasMoreHostilesThanFriendliesInBlast(Pawn pawn, float radius)
    {
        if (pawn?.Map == null)
            return false;

        int hostiles = 0;
        int friendlies = 0;

        foreach (IntVec3 c in GenRadial.RadialCellsAround(pawn.Position, radius, useCenter: true))
        {
            if (!c.InBounds(pawn.Map))
                continue;
            var things = c.GetThingList(pawn.Map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                if (t == null || t.Destroyed || t == pawn)
                    continue;

                // Only count living pawns — buildings register as "hostile" to an enemy
                // scarab via GenHostility, so counting them caused detonation next to empty
                // walls or doors. A scarab should only self-destruct near actual combatants.
                if (t is Pawn target)
                {
                    // Exclude wild insectoids: they are hostile to everyone (including Necrons)
                    // but are not colony targets. Counting them caused siege scarabs to detonate
                    // near hives in the staging zone or en route to the colony.
                    if (target.RaceProps.Insect && target.Faction != Faction.OfPlayer)
                        continue;

                    if (GenHostility.HostileTo(target, pawn))
                        hostiles++;
                    else
                        friendlies++;
                }
            }
        }

        return hostiles > friendlies;
    }

    internal static bool TryDetonate(Pawn pawn, ScarabSelfDestructProperties props)
    {
        if (pawn == null || pawn.Dead || pawn.Map == null)
            return false;

        int alive = AliveUnitCount(pawn);
        if (alive <= 0)
            return false;

        float scale = alive / 4f;
        float damage = props.baseDamage * scale;
        float radius = props.baseRadius * scale;

        // Enemy AI safety: only self-destruct when blast helps more than harms.
        // Player-issued detonation remains unrestricted.
        if (pawn.Faction != Faction.OfPlayer && !HasMoreHostilesThanFriendliesInBlast(pawn, radius))
            return false;

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
        return true;
    }
}

public class ScarabSelfDestruct : CompAbilityEffect
{
    public new ScarabSelfDestructProperties Props => (ScarabSelfDestructProperties)props;

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        Pawn pawn = parent.pawn;
        ScarabSelfDestructUtility.TryDetonate(pawn, Props);
    }
}
