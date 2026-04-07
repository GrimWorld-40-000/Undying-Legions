using RimWorld;
using Verse;

namespace NecronGeneUtil;

/// <summary>
/// Use-item serums do not run <see cref="Toils_Ingest.FinalizeIngest"/>, so nutrition is never applied to needs.
/// This mirrors that step: <see cref="Thing.Ingested"/> then add returned nutrition to food or <see cref="Need_Necrodermis"/>.
/// </summary>
public class CompUseEffect_NecrodermisInjectorIngest : CompUseEffect
{
    public override void DoEffect(Pawn user)
    {
        base.DoEffect(user);
        if (parent.Destroyed)
            return;

        Thing unit = parent.SplitOff(1);
        NecrodermisInjectorIngestApplier.ApplySingleUnitToPawn(user, unit);
    }
}
