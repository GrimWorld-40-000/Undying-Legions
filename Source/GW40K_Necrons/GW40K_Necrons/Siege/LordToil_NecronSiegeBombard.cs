using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Stage 3 — The Necron siege assault phase.
///
/// On enter:
///   • Spyder receives GW_UL_SpyderSiegeMode hediff (orange pulse, –40% speed, grants Particle Cannon).
///   • 4 kamikaze scarabs spawned and ordered to explode exterior defences.
///   • Defender scarabs switch to consume→produce mode (feeding necrodermis to the Spyder's fabricator).
///   • A message is sent: "{faction} Spyders are beginning their assault."
///
/// Each tick:
///   Phase A (production ready): spawn a kamikaze scarab and reset the production timer.
///   Phase B (recharging):       Particle Cannon fires via the Spyder's ability AI automatically.
///
/// Transitions out (handled by LordJob triggers):
///   • Spyder destroyed → Stage 4 assault.
///   • 50% pawn casualties → Stage 4 assault.
/// </summary>
public class LordToil_NecronSiegeBombard : LordToil
{
    // ── Cached DutyDef references ──────────────────────────────────────────────
    private static DutyDef _scarabDefendDuty;
    private static DutyDef _scarabAssaultDuty;
    private static DutyDef _spyderSiegeDuty;
    private static DutyDef ScarabDefendDuty  => _scarabDefendDuty  ??= DefDatabase<DutyDef>.GetNamedSilentFail("GW_UL_ScarabDefend")   ?? DutyDefOf.Defend;
    private static DutyDef ScarabAssaultDuty => _scarabAssaultDuty ??= DefDatabase<DutyDef>.GetNamedSilentFail("GW_UL_ScarabAssault")  ?? DutyDefOf.Defend;
    private static DutyDef SpyderSiegeDuty   => _spyderSiegeDuty   ??= DefDatabase<DutyDef>.GetNamedSilentFail("GW_UL_SpyderSiegeDuty") ?? DefDatabase<DutyDef>.GetNamedSilentFail("GW_UL_ScarabDefend") ?? DutyDefOf.Defend;

    // ── Constants ──────────────────────────────────────────────────────────────
    private const string SpyderKindDefName      = "UD_Necron_CanoptekSpyder";
    private const string ScarabKindDefName      = "GW_UL_ScarabSwarm";
    private const string SiegeModeHediffDefName = "GW_UL_SpyderSiegeMode";
    private const string BreaacherKindDefName   = "GW_UL_VekhThrall_Breacher";
    private const string SapperKindDefName      = "GW_UL_VekhThrall_Sapper";

    /// Scarab production interval ≈ mortar fire cadence (vanilla: ~60–120 s between shots).
    private const int ProductionIntervalTicks = 4500;  // ~75 s

    private const int InitialKamikazeCount = 4;
    private const int BodyguardCount       = 6;

    // Overflow: if >6 scarabs pile up near the staging center, send excess to assault.
    private const int OverflowCheckInterval = 1800; // 30 s
    private const int MaxScarabsAtCenter    = 6;
    private const float OverflowRadius      = 20f;
    private int overflowCheckCooldown       = OverflowCheckInterval;

    // Cannon reset removed — Comp_SpyderSiegeCannon cooldown disabled; warmup gates fire rate.

    // ── State ──────────────────────────────────────────────────────────────────
    private IntVec3 center;
    private bool    initialized;
    private int     productionCooldown = ProductionIntervalTicks;

    private Pawn             spyder;
    private List<Pawn>       bodyguards  = new();
    private HashSet<Pawn>    kamikazes   = new();

    // ── Accessors ──────────────────────────────────────────────────────────────

    /// <summary>True when the Spyder is gone — triggers Stage 4 from LordJob.</summary>
    public bool SpyderDestroyed => spyder == null || spyder.Dead || spyder.Destroyed;

    // ── Serialization ──────────────────────────────────────────────────────────

    public LordToil_NecronSiegeBombard() { }
    public LordToil_NecronSiegeBombard(IntVec3 center) { this.center = center; }

    // ── LordToil lifecycle ────────────────────────────────────────────────────

    public override void Init()
    {
        base.Init();
        if (initialized) return;
        initialized = true;

        FindSpyder();
        if (spyder == null) return;

        // Give Spyder siege mode hediff.
        HediffDef siegeDef = DefDatabase<HediffDef>.GetNamedSilentFail(SiegeModeHediffDefName);
        if (siegeDef != null && !spyder.health.hediffSet.HasHediff(siegeDef))
            spyder.health.AddHediff(siegeDef);

        // Message — historical: true so it persists in the message log.
        Messages.Message(
            "GW40K_NecronSiegeBegins".Translate(lord.faction.Name),
            MessageTypeDefOf.ThreatBig, historical: true);

        // Assign first BodyguardCount scarabs from the lord as bodyguards.
        AssignBodyguards();

        // Spawn 4 initial kamikaze scarabs.
        for (int i = 0; i < InitialKamikazeCount; i++)
            SpawnKamikazeScarab();

        // Opening bombardment — fire immediately when siege phase begins.
        FireSiegeCannon();
    }

    public override void LordToilTick()
    {
        if (SpyderDestroyed) return;

        // Scarab production
        productionCooldown--;
        if (productionCooldown <= 0)
        {
            productionCooldown = ProductionIntervalTicks;
            SpawnKamikazeScarab();
        }

        // Overflow check: if scarabs pile up in the staging area, dispatch excess.
        overflowCheckCooldown--;
        if (overflowCheckCooldown <= 0)
        {
            overflowCheckCooldown = OverflowCheckInterval;
            DispatchExcessScarabs();
        }


    }

    private void DispatchExcessScarabs()
    {
        float radiusSq = OverflowRadius * OverflowRadius;
        var atCenter = new List<Pawn>();
        foreach (Pawn p in lord.ownedPawns)
        {
            if (!ScarabRaidDutyUtility.IsScarab(p) || p.Dead || !p.Spawned) continue;
            if ((p.Position - center).LengthHorizontalSquared <= radiusSq)
                atCenter.Add(p);
        }

        if (atCenter.Count <= MaxScarabsAtCenter) return;

        // Move excess scarabs from bodyguards to kamikazes — UpdateAllDuties() assigns
        // ScarabAssaultDuty to kamikazes on the next tick.
        for (int i = MaxScarabsAtCenter; i < atCenter.Count; i++)
        {
            Pawn excess = atCenter[i];
            bodyguards.Remove(excess);
            kamikazes.Add(excess);
        }
    }

    public override void UpdateAllDuties()
    {
        if (spyder == null) FindSpyder();

        foreach (Pawn p in lord.ownedPawns)
        {
            // Custom DutyDefs (GW_UL_ScarabDefend / GW_UL_ScarabAssault) have no
            // SatisfyBasicNeedsAndWork, preventing the NullRef that vanilla Defend duty
            // causes when JobGiver_GetFood runs on a scarab with no food need.
            if (p == spyder)
            {
                // SpyderSiegeDuty puts JobGiver_AIAbilityFight for the particle cannon first,
                // before the wander node, so the cannon actually fires whenever it's off cooldown.
                p.mindState.duty = new PawnDuty(SpyderSiegeDuty, center);
            }
            else if (bodyguards.Contains(p))
            {
                IntVec3 focal = (spyder != null && spyder.Spawned) ? spyder.Position : center;
                p.mindState.duty = new PawnDuty(ScarabDefendDuty, focal);
            }
            else if (p.kindDef?.defName == SapperKindDefName)
            {
                // DutyDefOf.Sapper: vanilla dig-through-obstacles duty so Vekh sappers
                // actually tunnel through mountains when that's the shortest path.
                p.mindState.duty = new PawnDuty(DutyDefOf.Sapper, FindColonyTarget());
            }
            else if (p.kindDef?.defName == BreaacherKindDefName)
            {
                // DutyDefOf.Breaching: smash through constructed walls.
                p.mindState.duty = new PawnDuty(DutyDefOf.Breaching, FindColonyTarget());
            }
            else if (kamikazes.Contains(p))
            {
                p.mindState.duty = new PawnDuty(ScarabAssaultDuty, FindColonyTarget());
            }
            else
            {
                if (p.needs?.food != null)
                {
                    // Humanlike pawns with food needs (Vekh Thralls): always assault the colony.
                    // DutyDefOf.Defend at center was telling them to stay in the staging area,
                    // so Vekh who fell back during combat never re-engaged. AssaultColony with
                    // the colony target means UpdateAllDuties continuously points them at the
                    // fight — if they return to the staging area they immediately head back out.
                    p.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony, FindColonyTarget());
                }
                else
                {
                    // No-food Necron mechanoids: use no-needs duty to avoid NullRef [Ref 22F8EEF7].
                    p.mindState.duty = new PawnDuty(ScarabDefendDuty, center, 0f);
                }
            }
        }
    }

    public override void Cleanup()
    {
        // Remove ALL siege-mode hediffs (permanent siege-lord variant and manual timed variant)
        // so the Spyder can march freely when transitioning to the assault phase.
        if (spyder != null && !spyder.Dead && !spyder.Destroyed)
            HediffComp_SpyderSiegeMode.RemoveAll(spyder);
        base.Cleanup();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void FindSpyder()
    {
        spyder = lord.ownedPawns.FirstOrDefault(p => p.kindDef?.defName == SpyderKindDefName);
    }

    private void AssignBodyguards()
    {
        int count = 0;
        foreach (Pawn p in lord.ownedPawns)
        {
            if (count >= BodyguardCount) break;
            if (p.kindDef?.defName != ScarabKindDefName) continue;
            bodyguards.Add(p);
            count++;
        }
    }

    private void SpawnKamikazeScarab()
    {
        Pawn scarab = HediffComp_HiveFabricator.SpawnScarabAt(
            ScarabSpawnCell(),
            Map,
            lord.faction);

        if (scarab == null) return;

        lord.AddPawn(scarab);
        kamikazes.Add(scarab);

        // Immediately assign assault duty so the scarab marches to the colony
        // right away rather than waiting for the next UpdateAllDuties tick.
        if (scarab.mindState != null)
            scarab.mindState.duty = new PawnDuty(ScarabAssaultDuty, FindColonyTarget());

        // Each scarab production is accompanied by a cannon volley.
        FireSiegeCannon();
    }

    /// <summary>
    /// Orders the siege Spyder to fire one cannon volley at the colony.
    /// No-ops silently if the Spyder is busy, not in siege mode, or the target is out of range.
    /// </summary>
    private void FireSiegeCannon()
    {
        if (spyder == null || spyder.Dead || spyder.Destroyed || !spyder.Spawned) return;
        if (spyder.stances?.FullBodyBusy == true) return;

        Verb cannon = spyder.verbTracker?.AllVerbs?.Find(v => v is Verb_SpyderSiegeCannon);
        if (cannon == null || !cannon.Available()) return;

        IntVec3 target = FindColonyTarget();
        if (!target.IsValid || !target.InBounds(Map)) return;

        // requireLineOfSight=false on the verb so CanHitTarget is a pure range check.
        if (cannon.CanHitTarget(new LocalTargetInfo(target)))
            cannon.TryStartCastOn(new LocalTargetInfo(target));
    }

    /// <summary>
    /// Returns the best approximation of where the player's colony is on this map.
    /// Prefers the average position of spawned free colonists; falls back to the
    /// home-zone centroid; then falls back to map center.
    /// </summary>
    private IntVec3 FindColonyTarget()
    {
        Map map = Map;
        if (map == null) return center;

        // Average position of spawned free colonists.
        List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
        if (colonists.Count > 0)
            return new IntVec3(
                (int)colonists.Average(p => p.Position.x),
                0,
                (int)colonists.Average(p => p.Position.z));

        // Home-zone centroid.
        List<IntVec3> home = map.areaManager.Home?.ActiveCells.ToList();
        if (home?.Count > 0)
            return new IntVec3(
                (int)home.Average(c => c.x),
                0,
                (int)home.Average(c => c.z));

        return map.Center;
    }

    private IntVec3 ScarabSpawnCell()
    {
        IntVec3 root = spyder?.Position ?? center;
        if (!CellFinder.TryFindRandomCellNear(root, Map, 4,
                c => c.Standable(Map) && !c.Fogged(Map), out IntVec3 cell))
            cell = root;
        return cell;
    }
}
