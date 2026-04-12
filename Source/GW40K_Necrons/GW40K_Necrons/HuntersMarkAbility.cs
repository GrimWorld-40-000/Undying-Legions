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

        // Departure flash on the caster.
        EffecterDefOf.ForcedVisible.Spawn(parent.pawn.Position, parent.pawn.MapHeld);
        // Arrival flash on the target.
        EffecterDefOf.ForcedVisible.Spawn(targetPawn.Position, targetPawn.MapHeld);
    }

    // Prevent casting on already-marked targets (optional quality of life).
    public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
    {
        if (target.Thing is Pawn p && p.health.hediffSet.HasHediff(NecronDefOfs.GW40K_HuntersMark))
        {
            Messages.Message("GW40K_HuntersMark_AlreadyMarked".Translate(p.LabelShort),
                MessageTypeDefOf.RejectInput, false);
            return false;
        }
        return base.CanApplyOn(target, dest);
    }
}
