using RimWorld;
using Verse;

namespace NecronGeneUtil;

public class CompProperties_UsableNecrodermisInjector : CompProperties_Usable
{
    public CompProperties_UsableNecrodermisInjector()
    {
        compClass = typeof(CompUsable_NecrodermisInjectorTranshumanist);
    }
}

public class CompProperties_UsableNecrodermisPack : CompProperties_Usable
{
    public CompProperties_UsableNecrodermisPack()
    {
        compClass = typeof(CompUsable_NecrodermisNeedOnly);
    }
}
