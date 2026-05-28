using NecronGeneUtil;
using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons;

/// <summary>
/// Dispatches Produce-mode behaviour: pump necrodermis into a Spyder's Hive Fabricator,
/// or self-reproduce if controlled by a Cryptek / uncontrolled.
/// </summary>
public class JobGiver_ScarabProduce : ThinkNode_JobGiver
{
    private const float MinNecrodermisLevel = 0.9f; // 90 units on 0-1 scale

    protected override Job TryGiveJob(Pawn pawn)
    {
        // Yield to the Animal think tree while under attack.
        if (ScarabRaidDutyUtility.IsInRecentCombat(pawn)) return null;

        GameComponent_CanoptekConstructModes modes = GameComponent_CanoptekConstructModes.Current;
        if (modes == null) return null;
        if (modes.GetMode(pawn, ControlNodeMode.Consume) != ControlNodeMode.Produce) return null;

        // Produce mode disabled if not at full life
        if (!IsAtFullLife(pawn)) return null;

        Need_Necrodermis need = pawn.needs?.TryGetNeed<Need_Necrodermis>();
        if (need == null) return null;

        if (need.CurLevel < MinNecrodermisLevel)
        {
            Messages.Message("GW40K_Produce_InsufficientNecrodermis".Translate(pawn.LabelShortCap),
                pawn, MessageTypeDefOf.RejectInput, false);
            if (modes.GetAutoMode(pawn))
                modes.SetMode(pawn, ControlNodeMode.Consume);
            return null;
        }

        Pawn controller = HediffComp_ControlNodeTracker.GetControllerOfScarab(pawn);
        if (controller != null && ControlNodeUtility.IsSpyder(controller))
        {
            // Only pump if the Spyder's fabricator has Fill enabled and has space
            HediffComp_HiveFabricator fabricator = GetFabricator(controller);
            if (fabricator == null || !fabricator.autoRefuel || fabricator.stored >= fabricator.Props.maxStored)
                return null;
            JobDef pumpDef = DefDatabase<JobDef>.GetNamed("GW40K_Job_ScarabProduceToSpyder");
            return pumpDef != null ? JobMaker.MakeJob(pumpDef, controller) : null;
        }
        else
        {
            // Uncontrolled or Cryptek-controlled — self-reproduce
            JobDef reproduceDef = DefDatabase<JobDef>.GetNamed("GW40K_Job_ScarabReproduce");
            if (reproduceDef == null) return null;
            Job reproduceJob = JobMaker.MakeJob(reproduceDef);
            reproduceJob.SetTarget(TargetIndex.A, pawn); // progress bar tracks above self
            return reproduceJob;
        }
    }

    private static bool IsAtFullLife(Pawn pawn)
    {
        int total = HarmonyPatch_ScarabSwarmChassis.ScarabUnitSlotCount(pawn);
        if (total <= 0) return false;
        int present = 0;
        foreach (BodyPartRecord p in pawn.health.hediffSet.GetNotMissingParts())
            if (p.def.defName == HarmonyPatch_ScarabSwarmChassis.ScarabUnitPartDefName)
                present++;
        return present == total;
    }

    private static HediffComp_HiveFabricator GetFabricator(Pawn spyder)
    {
        HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("GW40K_HiveFabricator");
        if (def == null) return null;
        return spyder.health.hediffSet.GetFirstHediffOfDef(def)?.TryGetComp<HediffComp_HiveFabricator>();
    }
}
