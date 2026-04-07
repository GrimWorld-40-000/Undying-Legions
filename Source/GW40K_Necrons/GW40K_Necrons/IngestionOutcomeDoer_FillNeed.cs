using RimWorld;
using Verse;

namespace NecronMod
{
    // Fills a specified need by a flat amount when the item is ingested.
    // Usage in XML:
    //   <li Class="NecronMod.IngestionOutcomeDoer_FillNeed">
    //     <needDef>GW_UD_Necrodermis</needDef>
    //     <fillAmount>0.5</fillAmount>
    //   </li>
    public class IngestionOutcomeDoer_FillNeed : IngestionOutcomeDoer
    {
        public NeedDef needDef;
        public float fillAmount = 0.5f;

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            Need need = pawn.needs?.TryGetNeed(needDef);
            if (need == null) return;
            float add = fillAmount * ingestedCount;
            need.CurLevel = UnityEngine.Mathf.Clamp(need.CurLevel + add, 0f, need.MaxLevel);
        }
    }
}
