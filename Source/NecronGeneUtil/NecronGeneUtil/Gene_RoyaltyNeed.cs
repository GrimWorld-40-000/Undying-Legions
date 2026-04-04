using System.Collections.Generic;
using RimWorld;
using Verse;

namespace NecronGeneUtil;

public class Gene_RoyaltyNeed : Gene
{
    public GeneExtension_Necron modExtensions => base.def.GetModExtension<GeneExtension_Necron>();

    public override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        if (Gen.IsHashIntervalTick(base.pawn, 250)) CheckNeed();
    }

    public void CheckNeed()
    {
        if (modExtensions == null || GenList.NullOrEmpty<NeedDef>((IList<NeedDef>)modExtensions.needDefs)) return;
        bool flag = false;
        if (modExtensions.isGiveBuffIfAnyPassThreshold)
        {
            foreach (NeedDef nd in modExtensions.needDefs)
            {
                Need need = base.pawn.needs.TryGetNeed(nd);
                if (need != null && need.CurLevelPercentage >= modExtensions.threshold) { flag = true; break; }
            }
        }
        else
        {
            flag = true;
            foreach (NeedDef nd in modExtensions.needDefs)
            {
                Need need = base.pawn.needs.TryGetNeed(nd);
                if (need != null && need.CurLevelPercentage < modExtensions.threshold) { flag = false; break; }
            }
        }
        if (flag) GrantBuff(); else RemoveHediffWhenFail();
    }

    public void GrantBuff()
    {
        if (base.pawn.health.hediffSet.GetFirstHediffOfDef(modExtensions.hediffDef) != null) return;
        Hediff hediff = HediffMaker.MakeHediff(modExtensions.hediffDef, base.pawn, base.pawn.health.hediffSet.GetBodyPartRecord(modExtensions.part));
        base.pawn.health.AddHediff(hediff, null, (DamageInfo?)null);
    }

    public void RemoveHediffWhenFail()
    {
        Hediff hediff = base.pawn.health.hediffSet.GetFirstHediffOfDef(modExtensions.hediffDef, false);
        if (hediff != null) base.pawn.health.RemoveHediff(hediff);
    }
}
