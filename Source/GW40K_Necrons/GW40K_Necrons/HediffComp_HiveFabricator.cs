using System.Collections.Generic;
using System.Linq;
using NecronGeneUtil;
using RimWorld;
using UnityEngine;
using Verse;

namespace GW40K_Necrons;

public class HediffCompProperties_HiveFabricator : HediffCompProperties
{
    public float maxStored = 250f;
    public float replicateCost = 100f;
    public float replicateDurationTicks = 2500f; // 1 in-game hour
    public float necrodermisUnitsPerNeedLevel = 100f;

    public HediffCompProperties_HiveFabricator() => compClass = typeof(HediffComp_HiveFabricator);
}

public class HediffComp_HiveFabricator : HediffComp
{
    public float stored;
    public bool autoRefuel = true;
    public float minimumLevel;
    private long replicateFinishTick = -1L;

    public HediffCompProperties_HiveFabricator Props => (HediffCompProperties_HiveFabricator)props;
    public float StoredPct => stored / Props.maxStored;
    public bool IsReplicating => replicateFinishTick > 0;
    public int TicksUntilReplicate => IsReplicating ? Mathf.Max(0, (int)(replicateFinishTick - Find.TickManager.TicksGame)) : 0;

    public bool TryConsume(float units)
    {
        if (stored < units) return false;
        stored -= units;
        return true;
    }

    public void AddNecrodermis(float units)
    {
        stored = Mathf.Min(stored + units, Props.maxStored);
    }

    public bool TryStartReplication(out string failReason)
    {
        if (IsReplicating)
        {
            failReason = "GW40K_Replicate_AlreadyReplicating".Translate();
            return false;
        }

        HediffComp_ControlNodeTracker tracker = HediffComp_ControlNodeTracker.GetTracker(parent.pawn);
        if (tracker == null || tracker.BandwidthUsed + tracker.BandwidthCostPerScarab > tracker.BandwidthMax)
        {
            failReason = "GW40K_Replicate_NoBandwidth".Translate();
            return false;
        }

        if (stored < Props.replicateCost)
        {
            failReason = "GW40K_Replicate_NoNecrodermis".Translate(Props.replicateCost.ToString("F0"));
            return false;
        }

        stored -= Props.replicateCost;
        replicateFinishTick = Find.TickManager.TicksGame + (long)Props.replicateDurationTicks;
        failReason = null;
        return true;
    }

    // 50 necrodermis units passively fed into the fabricator per game-hour while siege mode is active.
    private const float SiegeRegenPerTick = 50f / 60000f;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        // Passive regen during siege mode — the locked-down Spyder channels energy
        // into the hive fabricator instead of locomotion.
        if (HediffComp_SpyderSiegeMode.IsSiegeMode(parent.pawn))
            stored = Mathf.Min(stored + SiegeRegenPerTick, Props.maxStored);

        if (replicateFinishTick <= 0) return;
        if (Find.TickManager.TicksGame < replicateFinishTick) return;

        replicateFinishTick = -1L;
        Pawn spyder = parent.pawn;
        if (spyder == null || spyder.Dead || spyder.Destroyed || !spyder.Spawned) return;
        if (parent.Part != null && !spyder.health.hediffSet.GetNotMissingParts().Contains(parent.Part)) return;

        SpawnAndAttachScarab(spyder);
    }

    public static Pawn SpawnScarabAt(IntVec3 cell, Map map, Faction faction)
    {
        PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed("GW_UL_ScarabSwarm");
        PawnGenerationRequest req = new PawnGenerationRequest(
            kind, faction, PawnGenerationContext.NonPlayer,
            forceGenerateNewPawn: true, canGeneratePawnRelations: false,
            colonistRelationChanceFactor: 0f);
        Pawn scarab = PawnGenerator.GeneratePawn(req);
        GenSpawn.Spawn(scarab, cell, map);
        // Newly produced scarabs start with 20 necrodermis (0.2 on 0-1 scale).
        Need_Necrodermis necroNeed = scarab.needs?.TryGetNeed<Need_Necrodermis>();
        if (necroNeed != null) necroNeed.CurLevel = 0.2f;
        return scarab;
    }

    public static void ApplyMissingUnits(Pawn scarab, int count)
    {
        if (count <= 0) return;
        var units = scarab.health.hediffSet.GetNotMissingParts()
            .Where(p => p.def.defName == HarmonyPatch_ScarabSwarmChassis.ScarabUnitPartDefName)
            .Take(count)
            .ToList();
        foreach (BodyPartRecord part in units)
            scarab.health.AddHediff(HediffDefOf.MissingBodyPart, part);
    }

    private void SpawnAndAttachScarab(Pawn spyder)
    {
        HediffComp_ControlNodeTracker tracker = HediffComp_ControlNodeTracker.GetTracker(spyder);
        if (tracker == null || tracker.BandwidthUsed + tracker.BandwidthCostPerScarab > tracker.BandwidthMax) return;

        IntVec3 cell = CellFinder.RandomClosewalkCellNear(spyder.Position, spyder.Map, 2);
        Pawn scarab = SpawnScarabAt(cell, spyder.Map, spyder.Faction);
        tracker.BindScarab(scarab);

        Messages.Message("GW40K_Replicate_Success".Translate(spyder.LabelShortCap),
            spyder, MessageTypeDefOf.PositiveEvent);
    }

    public override IEnumerable<Gizmo> CompGetGizmos()
    {
        Pawn p = parent.pawn;
        if (p?.Faction != Faction.OfPlayer) yield break;
        if (!p.Spawned || p.Dead) yield break;
        if (HediffComp_ControlNodeTracker.GetTracker(p) == null) yield break;
        yield return new Gizmo_HiveFabricator(this, p);
    }

    public override string CompLabelInBracketsExtra => IsReplicating
        ? "GW40K_HiveFabricator_Replicating".Translate(TicksUntilReplicate.ToStringTicksToPeriod())
        : null;

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref stored, "stored", 0f);
        Scribe_Values.Look(ref autoRefuel, "autoRefuel", true);
        Scribe_Values.Look(ref minimumLevel, "minimumLevel", 0f);
        Scribe_Values.Look(ref replicateFinishTick, "replicateFinishTick", -1L);
    }
}

// Initialises the HiveFabricator hediff on the abdomen when a Spyder first spawns.
public class CompProperties_HiveFabricatorInit : CompProperties
{
    public CompProperties_HiveFabricatorInit() => compClass = typeof(Comp_HiveFabricatorInit);
}

public class Comp_HiveFabricatorInit : ThingComp
{
    private static readonly string AbdomenDefName = "GW40K_Spyder_Abdomen";
    private static readonly string HiveFabricatorDefName = "GW40K_HiveFabricator";

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        if (respawningAfterLoad) return;
        if (parent is not Pawn pawn) return;

        HediffDef hiveDef = DefDatabase<HediffDef>.GetNamedSilentFail(HiveFabricatorDefName);
        if (hiveDef == null || pawn.health.hediffSet.HasHediff(hiveDef)) return;

        BodyPartRecord abdomen = pawn.health.hediffSet.GetNotMissingParts()
            .FirstOrDefault(p => p.def.defName == AbdomenDefName);
        pawn.health.AddHediff(hiveDef, abdomen);

        // Reveal the Hive Fabricator learning lesson when a Spyder joins the player.
        if (pawn.Faction == Faction.OfPlayer && NecronDefOfs.GW_UD_Concept_HiveFabricator != null)
            LessonAutoActivator.TeachOpportunity(NecronDefOfs.GW_UD_Concept_HiveFabricator, OpportunityType.GoodToKnow);
    }
}
