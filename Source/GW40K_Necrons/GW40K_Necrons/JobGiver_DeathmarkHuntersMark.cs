using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

// Enemy-only AI: fires Hunter's Mark against the current combat target
// with a 60% probability (midpoint of the 50-70% design range) when the
// ability is off cooldown and the target is in range and not already marked.
public class JobGiver_DeathmarkHuntersMark : ThinkNode_JobGiver
{
    private const float castChance = 0.60f;

    protected override Job TryGiveJob(Pawn pawn)
    {
        // Deathmark enemy only.
        if (pawn.def.defName != "GW40K_NecronDeathmark") return null;
        if (pawn.Faction == null || pawn.Faction.IsPlayer) return null;
        if (pawn.Downed || pawn.Dead) return null;

        Ability mark = pawn.abilities?.GetAbility(NecronDefOfs.GW40K_Deathmark_HuntersMark);
        if (mark == null || !mark.CanCast) return null;

        // Must have an active enemy target that is a pawn.
        if (pawn.mindState.enemyTarget is not Pawn target) return null;
        if (target.Downed || target.Dead) return null;

        // Don't re-mark an already-marked target.
        if (target.health.hediffSet.HasHediff(NecronDefOfs.GW40K_HuntersMark)) return null;

        // Target must be within ability range.
        if (!pawn.Position.InHorDistOf(target.Position, mark.def.verbProperties.range)) return null;

        if (!Rand.Chance(castChance)) return null;

        return mark.GetJob(target, LocalTargetInfo.Invalid);
    }
}
