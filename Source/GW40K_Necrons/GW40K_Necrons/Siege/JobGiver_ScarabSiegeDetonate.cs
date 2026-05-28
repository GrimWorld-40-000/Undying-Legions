using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Siege-assault scarabs: fire GW40K_ScarabSelfDestruct once enemies enter the blast
/// radius (5 cells). Mirrors the safety logic in ScarabSelfDestructUtility — no detonation
/// if friendly scarabs are within the blast area.
///
/// Runs as the highest-priority node in GW_UL_ScarabAssault so a scarab that has leapt
/// inside the defences detonates immediately on reaching its targets.
/// </summary>
public class JobGiver_ScarabSiegeDetonate : ThinkNode_JobGiver
{
    private const float EnemyScanRadius = 5f;
    private const float BlastRadius     = 4.9f;

    private static AbilityDef SelfDestructDef =>
        DefDatabase<AbilityDef>.GetNamedSilentFail("GW40K_ScarabSelfDestruct");

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!ScarabRaidDutyUtility.IsHostileRaider(pawn)) return null;

        AbilityDef def = SelfDestructDef;
        if (def == null) return null;

        Ability ability = pawn.abilities?.GetAbility(def);
        if (ability == null || !ability.CanCast) return null;

        // Only detonate when colony-owned pawns (player faction) are within blast radius.
        // The general HasMoreHostilesThanFriendliesInBlast check includes wild animals
        // in manhunter state, causing detonation in the staging area near animals.
        // The 4-second cooldown on the ability prevents instant retry after a cancel,
        // ensuring the next attempt is from a different position / against a new target.
        if (!HasColonyPawnInBlast(pawn, BlastRadius)) return null;
        if (ScarabRaidDutyUtility.HasFriendlyInRadius(pawn, pawn.Position, BlastRadius)) return null;

        return ability.GetJob(pawn, pawn);
    }

    private static bool HasColonyPawnInBlast(Pawn pawn, float radius)
    {
        float radiusSq = radius * radius;
        foreach (Pawn other in pawn.Map.mapPawns.AllPawnsSpawned)
        {
            if (other == pawn || other.Dead || other.Downed) continue;
            if (other.Faction != Faction.OfPlayer) continue;
            if ((other.Position - pawn.Position).LengthHorizontalSquared <= radiusSq)
                return true;
        }
        return false;
    }
}
