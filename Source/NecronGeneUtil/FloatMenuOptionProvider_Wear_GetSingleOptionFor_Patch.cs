using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace NecronGeneUtil
{
    [HarmonyPatch(typeof(FloatMenuOptionProvider_Wear), "GetSingleOptionFor")]
    public static class FloatMenuOptionProvider_Wear_GetSingleOptionFor_Patch
    {
        public static void Postfix(ref FloatMenuOption __result, Thing clickedThing, FloatMenuContext context)
        {
            if (clickedThing != null && clickedThing is Apparel apparel)
            {
                Pawn pawn = context.FirstSelectedPawn;
                if (!pawn.genes.HasActiveGene(FMJ_DefOf.GW_UD_ApparelRestriction))
                {
                    return;
                }
                GeneExtension_Necron modExtension = pawn.genes.GetGene(FMJ_DefOf.GW_UD_ApparelRestriction)?.def.GetModExtension<GeneExtension_Necron>();
                if (modExtension != null)
                {
                    if (!modExtension.allowedApparels.NotNullAndContains(clickedThing.def))
                    {
                        __result = new FloatMenuOption($"{pawn.genes.XenotypeLabel} can't wear {clickedThing.LabelShort}. reason: {FMJ_DefOf.GW_UD_ApparelRestriction.LabelCap}".CapitalizeFirst(), null);
                        return;
                    }
                }
                else
                {
                    modExtension = clickedThing.def.GetModExtension<GeneExtension_Necron>();
                    if (modExtension != null)
                    {
                        if (!modExtension.canBeWornByNecron)
                        {
                            __result = new FloatMenuOption($"{pawn.genes.XenotypeLabel} can't wear {clickedThing.LabelShort}. reason: {FMJ_DefOf.GW_UD_ApparelRestriction.LabelCap}".CapitalizeFirst(), null);
                        }
                    }
                }
            }
        }
    }
}
