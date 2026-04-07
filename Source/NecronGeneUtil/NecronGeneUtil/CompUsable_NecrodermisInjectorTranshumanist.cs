using RimWorld;
using Verse;

namespace NecronGeneUtil;

/// <summary>
/// Self-injection is limited to humanlikes with the Transhumanist trait; others use surgery.
/// </summary>
public class CompUsable_NecrodermisInjectorTranshumanist : CompUsable
{
    public override AcceptanceReport CanBeUsedBy(Pawn p, bool ignoreErrors = false, bool disposable = false)
    {
        if (p != null && p.RaceProps.Humanlike && p.story?.traits?.HasTrait(FMJ_DefOf.Transhumanist) != true)
            return new AcceptanceReport("GW_UD_TranshumanistOnly".Translate());
        return base.CanBeUsedBy(p, ignoreErrors, disposable);
    }
}
