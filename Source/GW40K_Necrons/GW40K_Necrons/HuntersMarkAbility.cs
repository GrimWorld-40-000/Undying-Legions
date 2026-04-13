using RimWorld;
using Verse;

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
            Messages.Message("GW40K_HuntersMark_NotEnemy".Translate(p.LabelShort),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }

        // Prevent casting on already-marked targets.
        if (p.health.hediffSet.HasHediff(NecronDefOfs.GW40K_HuntersMark))
        {
            Messages.Message("GW40K_HuntersMark_AlreadyMarked".Translate(p.LabelShort),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }

        return base.CanApplyOn(target, dest);
    }
}
