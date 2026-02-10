//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//using HarmonyLib;
//using Verse;

//#nullable disable
//namespace GW40K_Necrons;

//[HarmonyPatch(typeof(Pawn_HealthTracker), "ForceDeathrestOrComa")]
//public class HarmonyPatch_ForceDeathrestOrComa
//{
//    [HarmonyPrefix]
//    public static bool fix(Pawn ___pawn, Pawn_HealthTracker __instance)
//    {
//        //if (!___pawn.IsHashIntervalTick(600))
//        //{
//        //    Log.Warning("resorting to vanilla deathrest because hash interval tick");
//        //    return true;
//        //}

//        if (___pawn.genes == null ||
//            !___pawn.genes.HasActiveGene(NecronDefOfs.GW_UD_ResurrectionProtocol))
//        {
//            Log.Warning("resorting to vanilla deathrest because no gene");
//            return true;
//        }

//        if (___pawn.health.summaryHealth.SummaryHealthPercent >= 0.10f)
//        {
//            Log.Warning("resorting to vanilla deathrest because health is more than 10%");
//            return true;
//        }

//        if (___pawn.health.hediffSet.GetFirstHediffOfDef(
//            NecronDefOfs.GW40K_Necron_ResurrectionHediff) != null)
//        {
//            Log.Warning("resorting to vanilla deathrest because already has resurrection hediff");
//            return true;
//        }

//        if (___pawn.health.hediffSet.GetBrain() == null)
//        {
//            Log.Warning("resorting to vanilla deathrest because no brain");
//            return true;
//        }

//        Log.Warning("resurrection procced");
//        HealthUtility.AdjustSeverity(___pawn, NecronDefOfs.GW40K_Necron_ResurrectionHediff, 0.999f);
//        return false;
//    }
//}
