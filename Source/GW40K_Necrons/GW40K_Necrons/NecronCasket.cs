using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Stasis cycle length = (base hours × race factor) + injury extra; timer counts down each tick while powered, flicked on,
/// and <see cref="CompStasisCryptNecrodermisRefuelable"/> has fuel — <see cref="NecronStasisHealing.ApplyHealPulse"/> on an interval.
/// </summary>
public class NecronCasket : Building_CryptosleepCasket
{
    private const string NecrodermisNeedDefName = "GW_UD_Necrodermis";
    private int ticksToFinish = -1;

    /// <summary>True while a cycle is in progress but fuel, power, or flick prevents advancing (timer and healing hold).</summary>
    public bool StasisCyclePausedForFuelOrPower =>
        ticksToFinish > 0 && ContainedThing != null && !StasisProcessingAllowed();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref ticksToFinish, "necronStasisTicksToFinish", -1);
    }

    public override bool Accepts(Thing thing)
    {
        if (thing is not Pawn pawn) return false;
        return pawn.needs?.TryGetNeed(NecronDefOfs.GW40K_CoreFlux) != null;
    }

    public override void Open()
    {
        if (!this.HasAnyContents) return;
        this.EjectContents();
        if (this.openedSignal.NullOrEmpty()) return;
        Find.SignalManager.SendSignal(new Signal(this.openedSignal, this.Named("SUBJECT")));
    }

    public override void EjectContents()
    {
        foreach (Thing thing in (IEnumerable<Thing>)this.innerContainer)
        {
            if (thing is Pawn pawn)
                PawnComponentsUtility.AddComponentsForSpawn(pawn);
        }
        if (!this.Destroyed)
            SoundDefOf.CryptosleepCasket_Eject.PlayOneShot(SoundInfo.InMap(new TargetInfo(this.Position, this.Map)));
        this.innerContainer.TryDropAll(this.InteractionCell, this.Map, ThingPlaceMode.Near);
        this.contentsKnown = true;
    }

    // Keep vanilla "Open" command visible while occupied; active-cycle interruption is confirmed in gizmo action wrapper.
    public override bool CanOpen => this.ContainedThing != null;

    public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
    {
        this.ticksToFinish = thing is Pawn p
            ? NecronStasisUtility.StasisCycleTicksFor(p)
            : Mathf.Max(1, Mathf.RoundToInt(GenDate.TicksPerDay * (NecronStasisUtility.BaseStasisHours / 24f)));
        return base.TryAcceptThing(thing, true);
    }

    protected override void Tick()
    {
        base.Tick();
        if (this.ContainedThing == null) return;
        if (this.ticksToFinish > 0)
        {
            if (!this.StasisProcessingAllowed())
                return;
            CompStasisCryptNecrodermisRefuelable fuel = this.GetComp<CompStasisCryptNecrodermisRefuelable>();
            fuel?.BurnFuelForStasisProcessingTick();
            --this.ticksToFinish;
            if (this.ContainedThing is Pawn necroPawn)
                RefillNecrodermisDuringStasis(necroPawn);
            if (this.ContainedThing is Pawn healPawn && StasisHealIntervalTicks() > 0
                && this.IsHashIntervalTick(StasisHealIntervalTicks()))
            {
                NecronStasisSettingsDef s = NecronDefOfs.GW40K_NecronStasisSettings;
                float perDay = s != null && s.healPointsPerDayWhileInStasis > 0f
                    ? s.healPointsPerDayWhileInStasis
                    : 80f;
                float eff = NecronStasisHealing.DeathrestHealEfficiency(healPawn);
                float pts = perDay * eff * (StasisHealIntervalTicks() / (float)GenDate.TicksPerDay);
                NecronStasisHealing.ApplyHealPulse(healPawn, pts);
            }
            return;
        }
        this.ticksToFinish = -1;
        Pawn pawn = this.innerContainer.First<Thing>() as Pawn;
        if (pawn != null)
            pawn.needs.TryGetNeed(NecronDefOfs.GW40K_CoreFlux)?.SetInitialLevel();
        this.Open();
    }

    private static int StasisHealIntervalTicks()
    {
        NecronStasisSettingsDef s = NecronDefOfs.GW40K_NecronStasisSettings;
        if (s == null || s.healIntervalTicks <= 0)
            return 600;
        return s.healIntervalTicks;
    }

    private bool StasisProcessingAllowed()
    {
        CompStasisCryptNecrodermisRefuelable fuel = this.GetComp<CompStasisCryptNecrodermisRefuelable>();
        if (fuel != null && !fuel.HasFuel)
            return false;
        CompPowerTrader power = this.GetComp<CompPowerTrader>();
        if (power != null && !power.PowerOn)
            return false;
        CompFlickable flick = this.GetComp<CompFlickable>();
        if (flick != null && !flick.SwitchIsOn)
            return false;
        return true;
    }

    private static void RefillNecrodermisDuringStasis(Pawn pawn)
    {
        if (pawn?.needs == null)
            return;
        NeedDef necroNeedDef = DefDatabase<NeedDef>.GetNamedSilentFail(NecrodermisNeedDefName);
        if (necroNeedDef == null)
            return;
        Need need = pawn.needs.TryGetNeed(necroNeedDef);
        if (need == null || need.CurLevel >= 1f)
            return;

        float gainPerDay = NecronStasisFuelUtility.StasisNecrodermisUnitsBurnedPerDay()
            * NecronStasisFuelUtility.NecrodermisNutritionPerUnitFromSettings();
        need.CurLevel = Mathf.Min(1f, need.CurLevel + gainPerDay / GenDate.TicksPerDay);
    }

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn myPawn)
    {
        if (!this.Accepts((Thing)myPawn))
        {
            yield return new FloatMenuOption("Pawn is not Necron", (Action)null);
            yield break;
        }
        if (myPawn.IsQuestLodger())
        {
            yield return new FloatMenuOption((string)"CannotUseReason".Translate((NamedArgument)"CryptosleepCasketGuestsNotAllowed".Translate()), (Action)null);
            yield break;
        }
        foreach (FloatMenuOption item in CompFloatMenuOptionsSafe(myPawn))
            yield return item;
        if (this.innerContainer.Count != 0)
            yield break;
        if (!myPawn.CanReach((LocalTargetInfo)(Thing)this, PathEndMode.InteractionCell, Danger.Deadly))
        {
            yield return new FloatMenuOption((string)"CannotUseNoPath".Translate(), (Action)null);
            yield break;
        }
        JobDef jobDef = JobDefOf.EnterCryptosleepCasket;
        string label = "GW40K_EnterStasisCrypt".Translate();
        Action action = delegate
        {
            if (ModsConfig.BiotechActive)
            {
                if (!(myPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PsychicBond) is Hediff_PsychicBond bond)
                    || !ThoughtWorker_PsychicBondProximity.NearPsychicBondedPerson(myPawn, bond))
                {
                    myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, this), JobTag.Misc);
                }
                else
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "PsychicBondDistanceWillBeActive_Cryptosleep".Translate(
                            myPawn.Named("PAWN"),
                            ((Pawn)bond.target).Named("BOND")),
                        delegate { myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, this), JobTag.Misc); },
                        destructive: true));
                }
            }
            else
            {
                myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, this), JobTag.Misc);
            }
        };
        yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action), myPawn, this);
    }

    /// <summary>Mirrors <see cref="ThingWithComps.GetFloatMenuOptions"/> comp pass without calling <see cref="Building_CryptosleepCasket.GetFloatMenuOptions"/> (avoids vanilla enter label).</summary>
    private IEnumerable<FloatMenuOption> CompFloatMenuOptionsSafe(Pawn selPawn)
    {
        List<ThingComp> list = this.AllComps;
        if (list == null)
            yield break;
        List<FloatMenuOption> buffer = new List<FloatMenuOption>();
        for (int i = 0; i < list.Count; i++)
        {
            ThingComp comp = list[i];
            try
            {
                foreach (FloatMenuOption item in comp.CompFloatMenuOptions(selPawn))
                    buffer.Add(item);
            }
            catch (Exception ex)
            {
                Log.Error("Exception in CompFloatMenuOptions for " + comp?.GetType()?.ToString() + " of " + this + " at " + selPawn + ": " + ex);
            }
        }
        foreach (FloatMenuOption item in buffer)
            yield return item;
    }

    public override string GetInspectStringLowPriority()
    {
        if (this.ticksToFinish <= 0)
            return string.Empty;
        TaggedString line = "GW40K_StasisCryptTimeRemaining"
            .Translate(this.ticksToFinish.ToStringTicksToPeriod().Named("REMAINING"));
        if (this.StasisCyclePausedForFuelOrPower)
            line += "\n" + "GW40K_StasisCryptCyclePaused".Translate();
        return DefNameDisplayUtility.ReplaceDefNamesWithLabels(line.Resolve());
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        string openLabel = "OpenCryptosleepCasket".Translate();
        foreach (Gizmo gizmo in base.GetGizmos())
        {
            if (gizmo is Command_Action cmd && cmd.defaultLabel == openLabel)
            {
                Action original = cmd.action;
                cmd.action = delegate
                {
                    if (this.ticksToFinish > 0 && this.Faction == Faction.OfPlayer)
                    {
                        Pawn pawn = this.ContainedThing as Pawn;
                        TaggedString text = pawn != null
                            ? "GW40K_StasisCryptInterruptConfirm".Translate(pawn.Named("PAWN"))
                            : "GW40K_StasisCryptInterruptConfirmNoPawn".Translate();
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, original, destructive: true));
                    }
                    else
                    {
                        original();
                    }
                };
            }
            else if (gizmo is Command cmdAny
                && this.ContainedThing != null
                && cmdAny.defaultLabel == "CommandUninstall".Translate())
            {
                cmdAny.Disable("GW40K_StasisCryptCannotUninstallOccupied".Translate());
            }
            else if (gizmo is Command cmdReinstall
                && this.ContainedThing != null
                && cmdReinstall.defaultLabel == "CommandReinstall".Translate())
            {
                cmdReinstall.Disable("GW40K_StasisCryptCannotReinstallOccupied".Translate());
            }
            yield return gizmo;
        }

        if (DebugSettings.godMode && this.ContainedThing is Pawn && this.ticksToFinish > 0)
        {
            yield return new Command_Action
            {
                defaultLabel = "DEV: Finish stasis now",
                defaultDesc = "Fast-forwards the stasis cycle to complete safely in a few ticks.",
                action = delegate { this.ticksToFinish = 2; }
            };
        }
    }
}
