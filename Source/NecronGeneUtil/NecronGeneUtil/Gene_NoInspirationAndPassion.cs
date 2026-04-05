using System.Linq;
using RimWorld;
using Verse;

namespace NecronGeneUtil;

public class Gene_NoInspirationAndPassion : Gene
{
    public GeneExtension_Necron modExtension => base.def.GetModExtension<GeneExtension_Necron>();

    public override void PostAdd()
    {
        base.PostAdd();
        foreach (SkillRecord skill in base.pawn.skills.skills.ToList())
            if ((int)skill.passion > 0) skill.passion = (Passion)0;
        if (modExtension?.hediffDef == null) return;
        var hediff = base.pawn.health.hediffSet.GetFirstHediffOfDef(modExtension.hediffDef)
            ?? base.pawn.health.AddHediff(modExtension.hediffDef, base.pawn.health.hediffSet.GetBodyPartRecord(modExtension.part));
        hediff.Severity = modExtension.severityOnAdd;
    }

    public override void PostRemove()
    {
        base.PostRemove();
        if (modExtension?.hediffDef == null) return;
        var hediff = base.pawn.health.hediffSet.GetFirstHediffOfDef(modExtension.hediffDef);
        if (hediff != null) base.pawn.health.RemoveHediff(hediff);
    }
}
