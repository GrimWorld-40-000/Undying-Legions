using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Necron siege lord — four stages:
///   Stage 1 (Arrive):   Pawns travel to staging area.
///   Stage 2 (Drop):     Defensive structures fall from drop pods.
///   Stage 3 (Bombard):  Spyder enters siege mode; alternates scarab swarms / particle cannon.
///   Stage 4 (Assault):  Full assault — necrons do not retreat.
/// </summary>
public class LordJob_NecronSiege : LordJob
{
    private IntVec3 siegeCenter;
    private int     siegeStartTick;
    private float   raidPoints;

    public LordJob_NecronSiege() { }
    public LordJob_NecronSiege(IntVec3 center, float points = 0f)
    {
        siegeCenter    = center;
        siegeStartTick = Find.TickManager.TicksGame;
        raidPoints     = points;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref siegeCenter,    "siegeCenter");
        Scribe_Values.Look(ref siegeStartTick, "siegeStartTick");
        Scribe_Values.Look(ref raidPoints,     "raidPoints",    0f);
    }

    public override StateGraph CreateGraph()
    {
        StateGraph graph = new StateGraph();

        // ── Stage 1 (Drop): re-drop all raiders at siege center, guarantee Spyder,
        //                    spawn fortification ring.
        LordToil_NecronSiegeDrop dropToil = new LordToil_NecronSiegeDrop(siegeCenter, raidPoints);
        graph.AddToil(dropToil);

        // ── Stage 2 (Bombard): Spyder siege mode, scarab swarms, particle cannon.
        LordToil_NecronSiegeBombard bombToil = new LordToil_NecronSiegeBombard(siegeCenter);
        graph.AddToil(bombToil);

        // ── Stage 3 (Assault): all advance and engage. Uses GW_UL_NecronAssault duty
        //    so humanlike Necrons fight visible enemies rather than marching under fire.
        LordToil_NecronSiegeAssault assaultToil = new LordToil_NecronSiegeAssault();
        graph.AddToil(assaultToil);

        // ── Stage 4 (Final Assault): siege mode removed from Spyder, everyone charges.
        LordToil_NecronFinalAssault finalToil = new LordToil_NecronFinalAssault();
        graph.AddToil(finalToil);

        // ── Stage 4.5 (Finish-off): raiders hunt downed / reanimating Necrons before leaving.
        //    Inserted here so the withdraw outcome chain (steal/kidnap/exit) is never
        //    interrupted by finish-off jobs. Exits when clean or after a hard 30 s cap.
        //    The Harmony patch (HarmonyPatch_FinishOffNecronBeforeLeaving) forfeits for
        //    siege raiders so this toil is the sole driver of finish-off during the siege.
        LordToil_NecronFinishOff finishOffToil = new LordToil_NecronFinishOff();
        graph.AddToil(finishOffToil);

        // ── Stage 5a (Withdraw dispatch): rolls outcome, sends message, routes to branch.
        LordToil_NecronWithdraw withdrawDispatch = new LordToil_NecronWithdraw();
        graph.AddToil(withdrawDispatch);

        // ── Stage 5b branches ─────────────────────────────────────────────────
        // All three branches eventually reach the shared exit toil.
        LordToil_ExitMap    exitToil   = new LordToil_ExitMap(LocomotionUrgency.Jog, canDig: false, interruptCurrentJob: true);
        LordToil_NecronSteal  stealToil  = new LordToil_NecronSteal();
        LordToil_NecronKidnap kidnaptToil = new LordToil_NecronKidnap();
        graph.AddToil(exitToil);
        graph.AddToil(stealToil);
        graph.AddToil(kidnaptToil);

        // ── Transitions ───────────────────────────────────────────────────────

        // 1 → 2: wait for drop pods to land and pawn duties to settle.
        Transition t1 = new Transition(dropToil, bombToil);
        t1.AddTrigger(new Trigger_TicksPassed(1800));  // 30 s — pods land, then bombard begins
        graph.AddTransition(t1);

        // 2 → 3: Spyder destroyed, heavy casualties, OR 10-minute time limit.
        Transition t2 = new Transition(bombToil, assaultToil);
        t2.AddTrigger(new Trigger_TickCondition(
            () => bombToil.SpyderDestroyed,
            GenTicks.TicksPerRealSecond));
        t2.AddTrigger(new Trigger_FractionPawnsLost(0.5f));
        t2.AddTrigger(new Trigger_TicksPassed(36000)); // ~10 game-minutes maximum bombard
        graph.AddTransition(t2);

        // 2 or 3 → 4 (Final Assault): 6 game-hours elapsed AND all Vekh down.
        Transition tFinalFromBomb = new Transition(bombToil, finalToil);
        tFinalFromBomb.AddTrigger(new Trigger_NecronFinalAssault(siegeStartTick));
        graph.AddTransition(tFinalFromBomb);

        Transition tFinalFromAssault = new Transition(assaultToil, finalToil);
        tFinalFromAssault.AddTrigger(new Trigger_NecronFinalAssault(siegeStartTick));
        graph.AddTransition(tFinalFromAssault);

        // ── 4 → 4.5 (Finish-off) — all three exit triggers route here first ────
        // Phases 2 and 3 escalate to 4; phase 4 always passes through finish-off
        // before reaching withdraw so the outcome chain is never interrupted.
        // Vanilla uses 0.25-0.35 damage fraction and 26000-38000 tick timeout;
        // we use 2× those values for the longer siege engagement.

        // GameEnding fires from all active phases so the lord cleans up on scenario end.
        // finishOffToil included so a mid-cleanup scenario end also routes to withdraw.
        foreach (LordToil source in (LordToil[])[bombToil, assaultToil, finalToil, finishOffToil])
        {
            Transition t = new Transition(source, withdrawDispatch);
            t.AddTrigger(new Trigger_GameEnding());
            graph.AddTransition(t);
        }

        // a) 80% pawn casualties during final assault → finish-off → withdraw.
        {
            Transition t = new Transition(finalToil, finishOffToil);
            t.AddTrigger(new Trigger_FractionPawnsLost(0.8f));
            graph.AddTransition(t);
        }

        // b) Colony absorbs ~2.5× vanilla damage threshold → finish-off → withdraw.
        // Vanilla: FloatRange(0.25f, 0.35f). Necrons hold longer — they do not break easily.
        {
            Transition t = new Transition(finalToil, finishOffToil);
            float damageFraction = new FloatRange(0.60f, 0.75f).RandomInRange;
            var dmgTrigger = new Trigger_FractionColonyDamageTaken(damageFraction, 1800f);
            dmgTrigger.WithFilter(new TriggerFilter_MapExitable());
            t.AddTrigger(dmgTrigger);
            graph.AddTransition(t);
        }

        // c) Final assault time limit → finish-off → withdraw.
        {
            Transition t = new Transition(finalToil, finishOffToil);
            var timeTrigger = new Trigger_TicksPassed(new IntRange(52000, 76000).RandomInRange);
            timeTrigger.WithFilter(new TriggerFilter_MapExitable());
            t.AddTrigger(timeTrigger);
            graph.AddTransition(t);
        }

        // ── 4.5 → 5 (Withdraw): no targets remain OR 30 s hard cap ──────────
        {
            Map map = null; // captured lazily on first trigger check; safe — map is stable during a raid
            Transition t = new Transition(finishOffToil, withdrawDispatch);
            t.AddTrigger(new Trigger_TickCondition(
                () => !LordToil_NecronFinishOff.AnyTargetsRemain(map ??= lord.Map),
                GenTicks.TicksPerRealSecond));
            t.AddTrigger(new Trigger_TicksPassed(1800)); // 30 s hard cap
            graph.AddTransition(t);
        }

        // ── Dispatch → outcome branches (fire 1 tick after Init sets ChosenOutcome) ──
        Transition tContent = new Transition(withdrawDispatch, exitToil);
        tContent.AddTrigger(new Trigger_TickCondition(() => withdrawDispatch.ChosenOutcome == 0, 1));
        graph.AddTransition(tContent);

        Transition tSteal = new Transition(withdrawDispatch, stealToil);
        tSteal.AddTrigger(new Trigger_TickCondition(() => withdrawDispatch.ChosenOutcome == 1, 1));
        graph.AddTransition(tSteal);

        Transition tKidnap = new Transition(withdrawDispatch, kidnaptToil);
        tKidnap.AddTrigger(new Trigger_TickCondition(() => withdrawDispatch.ChosenOutcome == 2, 1));
        graph.AddTransition(tKidnap);

        // ── Branch → exit (after activity window) ────────────────────────────
        // Steal: 800 ticks (~13 s) — items are grabbed quickly.
        // Kidnap: 2400 ticks (~40 s) — needs time for Necrons to locate, reach,
        //         and pick up downed victims before exiting. The periodic
        //         UpdateAllDuties call in LordToil_NecronKidnap.LordToilTick means
        //         Necrons that were still in dangerous combat on entry will retry
        //         every 5 s and switch to Kidnap duty once the coast clears.
        Transition stealDone = new Transition(stealToil, exitToil);
        stealDone.AddTrigger(new Trigger_TicksPassed(800));
        graph.AddTransition(stealDone);

        Transition kidnaptDone = new Transition(kidnaptToil, exitToil);
        kidnaptDone.AddTrigger(new Trigger_TicksPassed(2400));
        graph.AddTransition(kidnaptDone);

        return graph;
    }
}
