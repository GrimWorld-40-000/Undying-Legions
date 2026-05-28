using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Necrons loot the colony before withdrawing. Pawns that can find a
/// Necron-appropriate item steal it and carry it to the map edge; those
/// that can't begin exiting immediately.
/// Necrons ignore food, apparel, and organic matter — they target weapons,
/// components, refined materials, and gauss cores.
/// </summary>
public class LordToil_NecronSteal : LordToil
{
    protected DutyDef DutyDef => DutyDefOf.Steal;

    public override bool AllowSatisfyLongNeeds => false;
    public override bool AllowSelfTend         => false;

    public override void UpdateAllDuties()
    {
        List<Thing> taken = null;
        foreach (Pawn p in lord.ownedPawns)
        {
            if (p.Dead || !p.Spawned) continue;

            if (TryFindItem(p, taken, out Thing target) && !GenAI.InDangerousCombat(p))
            {
                if (p.mindState.duty?.def != DutyDef)
                {
                    p.mindState.duty = new PawnDuty(DutyDef);
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
                (taken ??= new List<Thing>()).Add(target);
            }
            else
            {
                // Nothing to steal or in danger — begin exiting.
                if (p.mindState.duty?.def != DutyDefOf.ExitMapBest)
                    p.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBest);
            }
        }
    }

    private static bool TryFindItem(Pawn pawn, List<Thing> taken, out Thing item)
    {
        // Already carrying something — keep it.
        if (pawn.mindState.duty?.def == DutyDefOf.Steal && pawn.carryTracker.CarriedThing != null)
        {
            item = pawn.carryTracker.CarriedThing;
            return true;
        }

        if (!StealAIUtility.TryFindBestItemToSteal(pawn.Position, pawn.Map, 7f, out item, pawn, taken))
            return false;

        return item != null && IsNecronLoot(item);
    }

    /// <summary>
    /// Necrons ignore biological matter and personal gear.
    /// They take weapons, manufactured components, and refined materials.
    /// </summary>
    private static bool IsNecronLoot(Thing t)
    {
        if (t?.def == null) return false;
        if (t.def.IsIngestible) return false;                          // no food
        if (t.def.IsApparel)    return false;                          // no clothes
        if (t is Corpse)        return false;                          // no bodies
        if (t.def.thingCategories == null) return false;
        // Accept weapons, components, resources (steel, plasteel, gold, silver, uranium).
        return t.def.IsWeapon
            || t.def.IsWithinCategory(ThingCategoryDefOf.ResourcesRaw)
            || t.def.IsWithinCategory(ThingCategoryDefOf.Manufactured)
            || t.MarketValue >= 50f; // catch-all for high-value items
    }
}
