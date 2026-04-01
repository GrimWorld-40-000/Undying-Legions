using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;

namespace NecronMod
{
    [StaticConstructorOnStartup]
    public static class NecronHarmony
    {
        static NecronHarmony()
        {
            var harmony = new Harmony("com.necron.necrodermis");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Recipe_RemoveHediff), "ApplyOnPawn")]
    public static class Patch_ExcisionYield
    {
        [HarmonyPrefix]
        public static void Prefix(Pawn pawn, RecipeDef recipe, out float __state)
        {
            // Capture severity BEFORE the hediff is removed
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("Necron_NecrodermisGrowth"));
            __state = hediff?.Severity ?? 0f;
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, RecipeDef recipe, float __state)
        {
            // Only run for our specific recipe and if it was successful (pawn still has parts)
            if (recipe.defName == "ExciseNecrodermis" && __state > 0)
            {
                // Calculate Yield: e.g., Max 50 units at 1.0 severity
                int yieldCount = Mathf.RoundToInt(__state * 50f);

                if (yieldCount > 0)
                {
                    Thing necrodermis = ThingMaker.MakeThing(ThingDef.Named("GW40k_Necron_Necrodermis"));
                    necrodermis.stackCount = yieldCount;
                    GenPlace.TryPlaceThing(necrodermis, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    
                    Messages.Message($"Extracted {yieldCount} units of Necrodermis from the host.", 
                        pawn, MessageTypeDefOf.PositiveEvent);
                }
            }
        }
    }
}