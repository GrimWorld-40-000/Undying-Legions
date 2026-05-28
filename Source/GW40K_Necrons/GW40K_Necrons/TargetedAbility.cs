using RimWorld;
using Verse;
using System.Linq;

namespace GW40K_Necrons;

public class HuntersMarkAbilityProperties : CompProperties_AbilityEffect
{
    public HuntersMarkAbilityProperties() => compClass = typeof(HuntersMarkAbility);
}

public class HuntersMarkAbility : CompAbilityEffect
{
    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        base.Apply(target, dest);

        if (target.Thing is not Pawn targetPawn) return;

        // Remove existing mark first so the timer resets cleanly on re-cast.
        targetPawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_HuntersMark)?.pawn
            .health.RemoveHediff(targetPawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_HuntersMark));

        targetPawn.health.AddHediff(NecronDefOfs.GW40K_HuntersMark);

        // DefDatabase<EffecterDef>.GetNamed("PsycastPsychicEffect", errorOnFail: false)?.Spawn(targetPawn.Position, targetPawn.MapHeld).Cleanup();
    }

    public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
    {
        if (target.Thing is not Pawn p) return base.CanApplyOn(target, dest);

        // Enemy pawns only.
        if (!GenHostility.HostileTo(p, parent.pawn))
        {
            if (parent.pawn.Faction == Faction.OfPlayer)
                Messages.Message("GW40K_HuntersMark_NotEnemy".Translate(p.LabelShort),
                    MessageTypeDefOf.RejectInput, false);
            return false;
        }

        // Prevent casting on already-marked targets.
        if (p.health.hediffSet.HasHediff(NecronDefOfs.GW40K_HuntersMark))
        {
            if (parent.pawn.Faction == Faction.OfPlayer)
                Messages.Message("GW40K_HuntersMark_AlreadyMarked".Translate(p.LabelShort),
                    MessageTypeDefOf.RejectInput, false);
            return false;
        }

        return base.CanApplyOn(target, dest);
    }
}

// Stored on the shackled pawn's hediff to track which caster owns this bond.
public class HediffCompProperties_MindshackleBond : HediffCompProperties
{
    public HediffCompProperties_MindshackleBond() => compClass = typeof(HediffComp_MindshackleBond);
}

public class HediffComp_MindshackleBond : HediffComp
{
    public int casterID = -1;

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref casterID, "casterID", -1);
    }
}

public class MindshackleAbilityProperties : CompProperties_AbilityEffect
{
    public int maxHosts = 1;
    public ThingDef sourceItem; // if set, this apparel is destroyed after casting
    public MindshackleAbilityProperties() => compClass = typeof(MindshackleAbility);
}

public class MindshackleAbility : CompAbilityEffect
{
    private new MindshackleAbilityProperties Props => (MindshackleAbilityProperties)props;

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        base.Apply(target, dest);
        if (target.Thing is not Pawn targetPawn) return;

        // Remove existing hediff so re-cast resets cleanly.
        var existing = targetPawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_Mindshackle);
        if (existing != null) targetPawn.health.RemoveHediff(existing);

        targetPawn.SetFaction(parent.pawn.Faction);
        var hediff = targetPawn.health.AddHediff(NecronDefOfs.GW40K_Mindshackle);
        var bond = hediff.TryGetComp<HediffComp_MindshackleBond>();
        if (bond != null) bond.casterID = parent.pawn.thingIDNumber;

        if (Props.sourceItem != null)
        {
            var artifact = parent.pawn.apparel?.WornApparel
                .FirstOrDefault(a => a.def == Props.sourceItem);
            if (artifact != null && parent.pawn.apparel.TryDrop(artifact, out var dropped, parent.pawn.Position, forbid: false))
                dropped?.Destroy(DestroyMode.Vanish);
        }
    }

    public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
    {
        if (target.Thing is not Pawn p) return base.CanApplyOn(target, dest);

        if (!GenHostility.HostileTo(p, parent.pawn))
        {
            Messages.Message("GW40K_Mindshackle_NotEnemy".Translate(p.LabelShort),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }

        if (p.health.hediffSet.HasHediff(NecronDefOfs.GW40K_Mindshackle))
        {
            Messages.Message("GW40K_Mindshackle_AlreadyShackled".Translate(p.LabelShort),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }

        if (CountActiveHosts() >= Props.maxHosts)
        {
            Messages.Message("GW40K_Mindshackle_AtCapacity".Translate(Props.maxHosts),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }

        return base.CanApplyOn(target, dest);
    }

    private int CountActiveHosts()
    {
        int myID = parent.pawn.thingIDNumber;
        int count = 0;
        foreach (var map in Find.Maps)
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                var h = pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_Mindshackle);
                if (h?.TryGetComp<HediffComp_MindshackleBond>()?.casterID == myID)
                    count++;
            }
        return count;
    }
}

// Custom apparel class: grants GW40k_MindshackleArtifact ability on equip, removes on unequip.
// Replaces the Royalty-only equippedAbility field on ThingDef.
public class MindshackleArtifactApparel : Apparel
{
    private static AbilityDef ArtifactAbility =>
        DefDatabase<AbilityDef>.GetNamed("GW40k_MindshackleArtifact", errorOnFail: false);

    public void OnWorn(Pawn pawn)
    {
        var def = ArtifactAbility;
        if (def != null && pawn.abilities != null && pawn.abilities.GetAbility(def) == null)
            pawn.abilities.GainAbility(def);
    }

    public override void Notify_Unequipped(Pawn pawn)
    {
        base.Notify_Unequipped(pawn);
        var def = ArtifactAbility;
        if (def != null && pawn.abilities != null)
            pawn.abilities.RemoveAbility(def);
    }
}

// TEMPORARY: Hijack will be granted only by a future Technomancer (Cryptek psychic-class) gene; behavior and AI are placeholders.
public class HijackAbilityProperties : CompProperties_AbilityEffect
{
    public HijackAbilityProperties() => compClass = typeof(HijackAbility);
}

/// <summary>
/// TEMPORARY: Steal a Command Protocol link from a hostile commanded construct (unbind old commander, bind caster).
/// Intended for player / scripted use; <c>AbilityDef.aiCanUse</c> is false so raid AI does not spam this.
/// Replace when Technomancer + psychic-class design lands.
/// </summary>
public class HijackAbility : CompAbilityEffect
{
    public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
    {
        if (target.Thing is not Pawn p)
            return base.CanApplyOn(target, dest);

        Pawn cryptek = parent.pawn;
        if (cryptek == null)
            return base.CanApplyOn(target, dest);

        // TEMPORARY: no extra AI gating here — AbilityDef.aiCanUse=false keeps hostile pawns from auto-casting.
        if (!GenHostility.HostileTo(p, cryptek))
        {
            Messages.Message("GW40K_Hijack_MustBeHostile".Translate(p.LabelShort), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        if (HediffComp_NecronCommandTracker.GetCommanderOf(p) == null)
        {
            Messages.Message("GW40K_Hijack_NotCommanded".Translate(p.LabelShort), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        HediffComp_NecronCommandTracker cryptekTracker = HediffComp_NecronCommandTracker.GetTracker(cryptek);
        if (cryptekTracker == null)
        {
            Messages.Message("GW40K_Hijack_NoCommandProtocol".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        if (!cryptekTracker.HasBandwidthFor(p))
        {
            Messages.Message("GW40K_CommandBandwidthFull".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        return base.CanApplyOn(target, dest);
    }

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        base.Apply(target, dest);
        if (target.Thing is not Pawn targetPawn || parent.pawn == null)
            return;

        // TEMPORARY: direct tracker swap — no cooldown sync, no ideo/faction edge cases, no Spyder/scarab-specific rules yet.
        Pawn oldCommander = HediffComp_NecronCommandTracker.GetCommanderOf(targetPawn);
        HediffComp_NecronCommandTracker.GetTracker(oldCommander)?.UnbindMech(targetPawn);
        HediffComp_NecronCommandTracker.GetTracker(parent.pawn)?.BindMech(targetPawn);
    }
}
