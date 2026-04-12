using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

public class TeleportAbility : CompAbilityEffect
{
    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        base.Apply(target, dest);
        Pawn pawn = parent.pawn;
        IntVec3 cell = target.Cell;

        // Visual: flash at departure point
        EffecterDefOf.ForcedVisible.Spawn(pawn.Position, pawn.MapHeld);

        // Move
        pawn.Position = cell;

        // Apply trans-dimensional invulnerability for 2 seconds
        pawn.health.AddHediff(NecronDefOfs.GW40K_TransDimensional);

if (pawn.Faction != null && pawn.Faction.IsPlayer)
        {
            // Delay camera snap so it fires after the teleport visual settles.
            // 15 ticks ≈ 0.25 s at normal speed.
            DeferredActions.Schedule(15, () =>
            {
                if (pawn.Spawned) CameraJumper.TryJumpAndSelect(pawn);
            });
        }
        else
        {
            // Delay job interruption by 1 tick so the ability's own job driver
            // finishes cleanly before we force a re-evaluation.
            DeferredActions.Schedule(1, () =>
            {
                if (pawn.Spawned && !pawn.Dead)
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: true);
            });
        }
    }
}
