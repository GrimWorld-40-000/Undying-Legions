using System.Collections.Generic;
using RimWorld;
using Verse;

namespace NecronGeneUtil;

/// <summary>
/// Medical administration of <see cref="FMJ_DefOf.GW_NecrodermisInjector"/> for humanlikes who are not Transhumanist (they self-inject via gizmo).
/// </summary>
public class RecipeWorker_NecrodermisInjectorSurgery : Recipe_Surgery
{
    public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
    {
        if (!base.AvailableOnNow(thing, part))
            return false;
        if (thing is not Pawn pawn || !pawn.RaceProps.Humanlike)
            return false;
        if (pawn.story?.traits?.HasTrait(FMJ_DefOf.Transhumanist) == true)
            return false;
        return true;
    }

    protected override void OnSurgerySuccess(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
    {
        NecrodermisInjectorIngestApplier.ApplyVirtualInjectorDose(pawn);
    }
}
