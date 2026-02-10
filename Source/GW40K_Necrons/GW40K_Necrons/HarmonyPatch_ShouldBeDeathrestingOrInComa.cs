//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//using HarmonyLib;
//using Verse;

//#nullable disable
//namespace GW40K_Necrons;

//[HarmonyPatch(typeof(Pawn_HealthTracker), "ShouldBeDeathrestingOrInComa")]
//public class HarmonyPatch_ShouldBeDeathrestingOrInComa
//{
//    [HarmonyPostfix]
//    public static void fix(Pawn ___pawn, ref bool __result)
//    {
//        // the patch should only run if the pawn isn't already supposed to deathrest
//        if(__result)
//        {
//            return;
//        }

//        if (___pawn.genes == null ||
//            !___pawn.genes.HasActiveGene(NecronDefOfs.GW_UD_ResurrectionProtocol))
//            return;

//        if (___pawn.health.summaryHealth.SummaryHealthPercent >= 0.10f)
//            return;

//        if (___pawn.health.hediffSet.GetFirstHediffOfDef(
//                NecronDefOfs.GW40K_Necron_ResurrectionHediff) != null)
//            return;

//        if (___pawn.health.hediffSet.GetBrain() == null)
//            return;

//        __result = true;
//    }
//}
