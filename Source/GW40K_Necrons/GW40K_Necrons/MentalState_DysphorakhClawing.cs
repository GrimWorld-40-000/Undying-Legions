using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Compulsive Clawing mental state — three escalating stages that can end in Flayed One conversion.
///
/// Stage 1 (0 – 1.5 days):   4 × 25% scratch rolls per day.  Breakable normally.
/// Stage 2 (1.5 – 3.5 days): 6 × 25% scratch rolls per day.  Breakable normally.
/// Stage 3 (3.5 + days):     8 × 25% scratch rolls per day.  Breakable only by downing the pawn.
///
/// If the state is not broken before <see cref="FlayedOneConversionDays"/> days have elapsed,
/// the pawn's race changes to Flayed One and they enter Murderous Rage.
/// </summary>
public class MentalState_DysphorakhClawing : MentalState
{
    private int ticksElapsed;
    private int ticksSinceLastCheck;
    private bool stage2MessageSent;
    private bool stage3MessageSent;
    private bool conversionTriggered;

    private const float Stage1EndDays       = 1.5f;
    private const float Stage2EndDays       = 3.5f;
    private const float FlayedOneConversionDays = 6.0f;

    private float DaysElapsed => (float)ticksElapsed / GenDate.TicksPerDay;

    private int ChecksPerDay =>
        DaysElapsed < Stage1EndDays ? 4 :
        DaysElapsed < Stage2EndDays ? 6 : 8;

    // Stage 3 cannot be ended by social interaction — only by downing the pawn.
    protected override bool CanEndBeforeMaxDurationNow => DaysElapsed < Stage2EndDays;

    public override void MentalStateTick(int delta)
    {
        base.MentalStateTick(delta);

        if (conversionTriggered || pawn == null || pawn.Dead)
            return;

        ticksElapsed += delta;

        // Stage-transition notifications
        if (!stage2MessageSent && DaysElapsed >= Stage1EndDays)
        {
            stage2MessageSent = true;
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
                Messages.Message("GW40K_Dysphorakh_Clawing_Stage2".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.ThreatSmall);
        }

        if (!stage3MessageSent && DaysElapsed >= Stage2EndDays)
        {
            stage3MessageSent = true;
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
                Messages.Message("GW40K_Dysphorakh_Clawing_Stage3".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.ThreatBig);
        }

        // Flayed One conversion
        if (DaysElapsed >= FlayedOneConversionDays)
        {
            conversionTriggered = true;
            TriggerFlayedOneConversion();
            return;
        }

        // Scratch damage rolls — spaced evenly across the day
        ticksSinceLastCheck += delta;
        int ticksPerCheck = GenDate.TicksPerDay / ChecksPerDay;
        while (ticksSinceLastCheck >= ticksPerCheck)
        {
            ticksSinceLastCheck -= ticksPerCheck;
            if (Rand.Chance(0.25f))
                DealScratchDamage();
        }
    }

    private void DealScratchDamage()
    {
        // Untargeted cut — RimWorld picks a random body part, weighted toward the outer body.
        pawn.TakeDamage(new DamageInfo(DamageDefOf.Cut, 5f, 0f, -1f, pawn));
    }

    private void TriggerFlayedOneConversion()
    {
        ThingDef flayedDef = DefDatabase<ThingDef>.GetNamedSilentFail("GW40K_NecronFlayedOne");

        if (flayedDef != null)
        {
            DropWeaponAndShield();
            pawn.def = flayedDef;
            pawn.Drawer.renderer.SetAllGraphicsDirty();
        }

        if (PawnUtility.ShouldSendNotificationAbout(pawn))
            Messages.Message(
                "GW40K_Dysphorakh_FlayedConversion".Translate(pawn.Named("PAWN")),
                pawn, MessageTypeDefOf.ThreatBig);

        NotifyWitnesses();

        // End the clawing state before starting murderous rage.
        RecoverFromState();

        MentalStateDef rageDef = DefDatabase<MentalStateDef>.GetNamedSilentFail("MurderousRage");
        if (rageDef != null)
            pawn.mindState?.mentalStateHandler.TryStartMentalState(
                rageDef, forced: true, forceWake: true, causedByMood: false);
    }

    private void DropWeaponAndShield()
    {
        // Drop held weapon
        if (pawn.equipment?.Primary != null)
            pawn.equipment.TryDropEquipment(
                pawn.equipment.Primary, out _, pawn.Position, forbid: false);

        // Drop necron shield (apparel slot)
        if (pawn.apparel == null) return;
        ThingDef shieldDef = DefDatabase<ThingDef>.GetNamedSilentFail("GM40k_Necron_Shield");
        if (shieldDef == null) return;

        Apparel shield = null;
        var worn = pawn.apparel.WornApparel;
        for (int i = 0; i < worn.Count; i++)
        {
            if (worn[i].def == shieldDef) { shield = worn[i]; break; }
        }
        if (shield == null) return;

        pawn.apparel.Remove(shield);
        GenPlace.TryPlaceThing(shield, pawn.Position, pawn.Map, ThingPlaceMode.Near);
    }

    private void NotifyWitnesses()
    {
        if (pawn.Map == null) return;

        ThoughtDef normalThought    = NecronDefOfs.GW40K_WitnessedFlayerVirus;
        ThoughtDef psychoThought    = NecronDefOfs.GW40K_WitnessedFlayerVirus_Psychopath;
        TraitDef   psychopathTrait  = DefDatabase<TraitDef>.GetNamedSilentFail("Psychopath");
        GeneDef    lifelessGene     = DefDatabase<GeneDef>.GetNamedSilentFail("GW_UD_LifeLess");

        if (normalThought == null && psychoThought == null) return;

        IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn witness = pawns[i];
            if (witness == pawn || witness.Dead || !witness.Spawned) continue;
            if (!witness.RaceProps.IsFlesh) continue;
            if (witness.Position.DistanceTo(pawn.Position) > 6f) continue;
            // Exclude Necrons (all carry GW_UD_LifeLess)
            if (lifelessGene != null && (witness.genes?.HasActiveGene(lifelessGene) ?? false)) continue;
            if (witness.needs?.mood?.thoughts?.memories == null) continue;

            bool isPsychopath = psychopathTrait != null
                && (witness.story?.traits?.HasTrait(psychopathTrait) ?? false);

            ThoughtDef thought = isPsychopath ? psychoThought : normalThought;
            if (thought != null)
                witness.needs.mood.thoughts.memories.TryGainMemory(thought);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref ticksElapsed,       "clawingTicksElapsed",       0);
        Scribe_Values.Look(ref ticksSinceLastCheck, "clawingTicksSinceCheck",    0);
        Scribe_Values.Look(ref stage2MessageSent,   "clawingStage2Sent",         false);
        Scribe_Values.Look(ref stage3MessageSent,   "clawingStage3Sent",         false);
        Scribe_Values.Look(ref conversionTriggered, "clawingConversionTriggered",false);
    }
}
