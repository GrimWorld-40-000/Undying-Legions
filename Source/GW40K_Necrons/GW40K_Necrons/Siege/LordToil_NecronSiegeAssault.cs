using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Stage 3 assault lord toil — all units advance and fight.
///
/// Assigns GW_UL_NecronAssault to humanlike Necrons (JobGiver_AIFightEnemy first,
/// so they engage ranged targets rather than marching silently under fire).
/// Canoptek constructs get ScarabAssaultDuty (Animal subtree handles combat).
///
/// Does NOT remove siege-mode hediffs or send messages — that is reserved for
/// LordToil_NecronFinalAssault (Stage 4).
/// </summary>
public class LordToil_NecronSiegeAssault : LordToil
{
    private bool initialized;

    private static DutyDef _necronAssaultDuty;
    private static DutyDef _scarabAssaultDuty;

    private static DutyDef NecronAssaultDuty =>
        _necronAssaultDuty ??= DefDatabase<DutyDef>.GetNamedSilentFail("GW_UL_NecronAssault")
                            ?? DutyDefOf.AssaultColony;

    private static DutyDef ScarabAssaultDuty =>
        _scarabAssaultDuty ??= DefDatabase<DutyDef>.GetNamedSilentFail("GW_UL_ScarabAssault")
                            ?? DutyDefOf.Defend;

    public override void Init()
    {
        base.Init();
        if (initialized) return;
        initialized = true;

        // Drop siege mode from all Spyders so they can march during the assault phase.
        foreach (Pawn p in lord.ownedPawns)
            HediffComp_SpyderSiegeMode.RemoveAll(p);

        // Announce the escalation — logged to message history so players can review it.
        Messages.Message(
            "GW40K_NecronSiegeAssault".Translate(lord.faction.Name),
            MessageTypeDefOf.ThreatBig, historical: true);
    }

    public override void UpdateAllDuties()
    {
        IntVec3 target = RaidStrategyWorker_NecronSiege.FindColonyCenter(Map);
        AssignNecronAssaultDuties(lord, target, NecronAssaultDuty, ScarabAssaultDuty);
    }

    /// <summary>
    /// Shared duty-assignment logic used by both Stage 3 and Stage 4.
    /// Humanlike Necrons → <paramref name="necronDuty"/>;
    /// Canoptek constructs → <paramref name="scarabDuty"/>;
    /// Canoptek Spyder (has <see cref="Verb_SpyderParticleBeamer"/>) → standoff duty so it
    /// fires the beamer from range instead of marching into melee with the scarabs.
    /// </summary>
    internal static void AssignNecronAssaultDuties(
        Lord lord, IntVec3 target,
        DutyDef necronDuty, DutyDef scarabDuty)
    {
        DutyDef spyderDuty = DefDatabase<DutyDef>.GetNamedSilentFail("GW_UL_SpyderRaidStandoff");
        IntVec3 spyderStandoff = IntVec3.Invalid; // calculated lazily on first Spyder encountered

        foreach (Pawn p in lord.ownedPawns)
        {
            DutyDef duty;
            IntVec3 focus = target;

            if (!p.RaceProps.Humanlike
                && spyderDuty != null
                && JobGiver_SpyderRaidStandoff.HasParticleBeamer(p))
            {
                duty = spyderDuty;
                // Calculate standoff position once; all Spyders in this lord share it.
                if (!spyderStandoff.IsValid)
                    spyderStandoff = JobGiver_SpyderRaidStandoff.CalcStandoffPos(
                        lord.Map, target, p.Position,
                        JobGiver_SpyderRaidStandoff.StandoffDist);
                focus = spyderStandoff.IsValid ? spyderStandoff : target;
            }
            else
            {
                duty = p.RaceProps.Humanlike ? necronDuty : scarabDuty;
            }

            p.mindState.duty = new PawnDuty(duty, focus);
        }
    }
}
