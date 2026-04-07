using RimWorld;
using Verse;

namespace NecronGeneUtil;

/// <summary>
/// Raw necrodermis is only self-usable by pawns with <see cref="Need_Necrodermis"/>.
/// </summary>
public class CompUsable_NecrodermisNeedOnly : CompUsable
{
    public override AcceptanceReport CanBeUsedBy(Pawn p, bool ignoreErrors = false, bool disposable = false)
    {
        if (p?.needs?.TryGetNeed<Need_Necrodermis>() == null)
            return new AcceptanceReport("GW_UD_NecrodermisNeedOnly".Translate());
        return base.CanBeUsedBy(p, ignoreErrors, disposable);
    }
}
