using RimWorld;
using Verse;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Fires when BOTH conditions are met:
///   1. At least 6 game-hours have elapsed since the trigger was created.
///   2. Every Vekh Thrall in the lord is incapacitated or dead.
///
/// Checked once per second to avoid per-tick overhead.
/// </summary>
public class Trigger_NecronFinalAssault : Trigger
{
    private int startTick;

    private const int MinElapsedTicks  = GenDate.TicksPerHour * 6; // 6 game-hours
    private const int CheckIntervalTicks = GenTicks.TicksPerRealSecond;

    /// <param name="startTick">The tick at which the siege started. Pass from
    /// LordJob_NecronSiege.ExposeData so the timer survives save/load.</param>
    public Trigger_NecronFinalAssault(int startTick)
    {
        this.startTick = startTick;
    }

    public override bool ActivateOn(Lord lord, TriggerSignal signal)
    {
        if (signal.type != TriggerSignalType.Tick) return false;
        if (Find.TickManager.TicksGame % CheckIntervalTicks != 0) return false;
        if (Find.TickManager.TicksGame - startTick < MinElapsedTicks) return false;

        // All Vekh Thralls must be incapacitated or dead.
        foreach (Pawn p in lord.ownedPawns)
        {
            if (IsVekhThrall(p) && !p.Dead && !p.Downed)
                return false;
        }
        return true;
    }

    private static bool IsVekhThrall(Pawn p) =>
        p?.kindDef?.defName?.StartsWith("GW_UL_VekhThrall") == true;

}
