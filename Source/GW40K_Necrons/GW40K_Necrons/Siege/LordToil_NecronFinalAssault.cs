using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Final siege phase — triggered after 6 game-hours with all Vekh down.
/// On enter: removes GW_UL_SpyderSiegeMode from all Spyders, sends a threat message.
/// Every tick: assigns AssaultColony to humanlike-with-food pawns; ScarabAssaultDuty
/// to no-food mechanoids and scarabs (avoids SatisfyBasicNeedsAndWork NullRef).
/// </summary>
public class LordToil_NecronFinalAssault : LordToil
{
    private bool initialized;

    private static DutyDef _scarabAssaultDuty;
    private static DutyDef ScarabAssaultDuty =>
        _scarabAssaultDuty ??= DefDatabase<DutyDef>.GetNamedSilentFail("GW_UL_ScarabAssault") ?? DutyDefOf.Defend;

    public override void Init()
    {
        base.Init();
        if (initialized) return;
        initialized = true;

        // Remove ALL siege-mode hediffs from every pawn in the lord — covers both the
        // permanent siege-lord variant and the manual timed variant.
        foreach (Pawn p in lord.ownedPawns)
            HediffComp_SpyderSiegeMode.RemoveAll(p);

        // historical: true — logged so players can review the siege timeline.
        Messages.Message(
            "GW40K_NecronFinalAssault".Translate(lord.faction.Name),
            MessageTypeDefOf.ThreatBig, historical: true);
    }

    public override void UpdateAllDuties()
    {
        IntVec3 target = FindColonyTarget();
        // Humanlike → DutyDefOf.AssaultColony. LordDuty is now processed by the direct
        //   ThinkNode_Subtree(LordDuty) added to GW40K_Necron_ThinkTree, which fires for
        //   non-voluntarily-joinable lords (ThinkNode_JoinVoluntarilyJoinableLord was
        //   silently skipping our raid lord since LordJob_NecronSiege is not a VoluntarilyJoinable).
        // Canoptek → GW_UL_ScarabAssault: targetAcquireRadius=10 prevents scarabs from stopping
        //   to fight colonists at the staging area perimeter (GW_UL_NecronFinalAssault had
        //   acquire=40 which blocked the march). Includes detonate and leap job givers for the
        //   final push. targetKeepRadius=14 + wanderRadius=1 keep them at the colony center.
        LordToil_NecronSiegeAssault.AssignNecronAssaultDuties(
            lord, target, DutyDefOf.AssaultColony, ScarabAssaultDuty);
    }

    private IntVec3 FindColonyTarget()
    {
        Map map = Map;
        if (map == null) return IntVec3.Zero;
        return RaidStrategyWorker_NecronSiege.FindColonyCenter(map);
    }
}
