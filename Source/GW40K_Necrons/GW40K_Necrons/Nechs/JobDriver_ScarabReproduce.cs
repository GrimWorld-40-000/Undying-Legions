using System.Collections.Generic;
using NecronGeneUtil;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Self-reproduction: the scarab holds position for 30 seconds then spawns a new scarab
/// whose completeness is determined by a random roll.
/// </summary>
public class JobDriver_ScarabReproduce : JobDriver
{
    private const int ReproduceDurationTicks = 3600; // ~1.44 in-game hours
    private const float NecrodermisCost = 0.9f; // 90 units on 0-1 scale

    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        Toil reproduce = ToilMaker.MakeToil("ScarabReproduce");
        reproduce.defaultDuration = ReproduceDurationTicks;
        reproduce.defaultCompleteMode = ToilCompleteMode.Delay;
        reproduce.WithProgressBar(TargetIndex.A, () =>
            1f - (float)pawn.jobs.curDriver.ticksLeftThisToil / ReproduceDurationTicks);

        reproduce.finishActions = new List<System.Action>
        {
            () => SpawnResult(pawn)
        };

        // If attacked, cancel the job (auto-repair switch is handled by the Harmony patch)
        reproduce.AddFailCondition(() => pawn.Dead || pawn.Downed);

        yield return reproduce;
    }

    private static void SpawnResult(Pawn scarab)
    {
        if (scarab == null || scarab.Dead || scarab.Destroyed || !scarab.Spawned) return;

        // Consume necrodermis
        Need_Necrodermis need = scarab.needs?.TryGetNeed<Need_Necrodermis>();
        if (need != null)
            need.CurLevel = Mathf.Max(0f, need.CurLevel - NecrodermisCost);

        // RNG determines spawn quality
        float roll = Rand.Value;
        int missing = roll >= 0.75f ? 0 :
                      roll >= 0.50f ? 1 :
                      roll >= 0.25f ? 2 : 3;

        IntVec3 cell = CellFinder.RandomClosewalkCellNear(scarab.Position, scarab.Map, 2);
        Pawn newScarab = HediffComp_HiveFabricator.SpawnScarabAt(cell, scarab.Map, scarab.Faction);
        HediffComp_HiveFabricator.ApplyMissingUnits(newScarab, missing);

        Messages.Message("GW40K_ScarabReplicated_Success".Translate(scarab.LabelShortCap),
            newScarab, MessageTypeDefOf.PositiveEvent);

        // Switch back to Consume immediately if auto mode is on.
        GameComponent_CanoptekConstructModes modes = GameComponent_CanoptekConstructModes.Current;
        if (modes != null && modes.GetAutoMode(scarab))
            modes.SetMode(scarab, ControlNodeMode.Consume);
    }
}
