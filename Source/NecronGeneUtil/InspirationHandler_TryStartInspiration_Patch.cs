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
    [HarmonyPatch(typeof(InspirationHandler), "TryStartInspiration")]
    public static class InspirationHandler_TryStartInspiration_Patch
    {
        public static bool Prefix(ref bool __result, InspirationHandler __instance, InspirationDef def, string reason = null, bool sendLetter = true)
        {
            if (__instance == null || __instance.pawn == null)
            {
                return true;
            }
            if (__instance.pawn.genes.HasActiveGene(FMJ_DefOf.GW_UD_LifeLess))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
