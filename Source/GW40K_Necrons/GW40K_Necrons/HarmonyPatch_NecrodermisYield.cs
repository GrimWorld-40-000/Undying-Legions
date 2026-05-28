using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NecronMod
{
    // Drops necrodermis yield when the host pawn dies with the seed still in them.
    // Notify_PawnKilled fires before the hediff is stripped, so severity is still valid.
    // This does NOT fire for surgical extraction deaths because Recipe_RemoveHediff
    // removes the hediff (and thus this comp) before pawn.Kill() is called.
    public class HediffCompProperties_NecrodermisDeathDrop : HediffCompProperties
    {
        public HediffCompProperties_NecrodermisDeathDrop()
        {
            compClass = typeof(HediffComp_NecrodermisDeathDrop);
        }
    }

    public class HediffComp_NecrodermisDeathDrop : HediffComp
    {
        public override void Notify_PawnKilled()
        {
            int yieldCount = Mathf.RoundToInt(parent.Severity * 50f);
            if (yieldCount <= 0) return;

            Map map = Pawn.MapHeld;
            IntVec3 pos = Pawn.PositionHeld;
            if (map == null) return;

            Thing necrodermis = ThingMaker.MakeThing(ThingDef.Named("GW40k_Necron_Necrodermis"));
            necrodermis.stackCount = yieldCount;
            GenPlace.TryPlaceThing(necrodermis, pos, map, ThingPlaceMode.Near);
            Messages.Message(
                $"Necrodermis colonization on {Pawn.LabelShort} yielded {yieldCount} units.",
                new TargetInfo(pos, map), MessageTypeDefOf.NeutralEvent);
        }
    }


    [HarmonyPatch(typeof(Recipe_RemoveHediff), "ApplyOnPawn")]
    public static class Patch_ExcisionYield
    {
        [HarmonyPrefix]
        public static void Prefix(Recipe_RemoveHediff __instance, Pawn pawn, out float __state)
        {
            // Capture severity BEFORE the hediff is removed.
            // recipe is a field on the RecipeWorker instance, not a method parameter in 1.6.
            if (__instance.recipe.defName != "ExciseNecrodermis") { __state = 0f; return; }
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("Necron_NecrodermisGrowth"));
            __state = hediff?.Severity ?? 0f;
        }

        [HarmonyPostfix]
        public static void Postfix(Recipe_RemoveHediff __instance, Pawn pawn, float __state)
        {
            if (__instance.recipe.defName != "ExciseNecrodermis" || __state <= 0)
                return;

            // Spawn yield before any death check so necrodermis appears regardless
            int yieldCount = Mathf.RoundToInt(__state * 50f);
            if (yieldCount > 0)
            {
                Thing necrodermis = ThingMaker.MakeThing(ThingDef.Named("GW40k_Necron_Necrodermis"));
                necrodermis.stackCount = yieldCount;
                GenPlace.TryPlaceThing(necrodermis, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                Messages.Message($"Extracted {yieldCount} units of necrodermis from the host.",
                    pawn, MessageTypeDefOf.PositiveEvent);
            }

            // Stage 2 (0.30–0.59): 20% chance of minor internal damage
            if (__state >= 0.30f && __state < 0.60f)
            {
                if (Rand.Value < 0.20f)
                    DamageRandomPart(pawn, BodyPartDepth.Inside, minDamage: 3, maxDamage: 8);
            }
            // Stage 4 (0.85+): neural integration is total — extraction always kills the host
            else if (__state >= 0.85f)
            {
                Messages.Message("The necrodermis had fully integrated with the host's nervous system. The extraction was fatal.",
                    pawn, MessageTypeDefOf.NegativeEvent);
                pawn.Kill(null);
            }
            // Stage 3 (0.65–0.84): scaling death chance + internal/external damage
            else if (__state >= 0.65f)
            {
                // 50% chance of significant internal damage
                if (Rand.Value < 0.50f)
                    DamageRandomPart(pawn, BodyPartDepth.Inside, minDamage: 8, maxDamage: 20);
                // 25% chance of external damage
                if (Rand.Value < 0.25f)
                    DamageRandomPart(pawn, BodyPartDepth.Outside, minDamage: 5, maxDamage: 12);

                // Scaling death chance — 1% at 0.65, 20% at 0.84
                float deathChance = Mathf.InverseLerp(0.65f, 0.84f, __state) * 0.19f + 0.01f;
                if (Rand.Value < deathChance)
                {
                    Messages.Message("The extraction trauma proved fatal to the host.",
                        pawn, MessageTypeDefOf.NegativeEvent);
                    pawn.Kill(null);
                }
            }
        }

        // Damages a random body part of the given depth. Uses SurgicalCut to mirror
        // vanilla surgery complication behaviour.
        private static void DamageRandomPart(Pawn pawn, BodyPartDepth depth, int minDamage, int maxDamage)
        {
            var candidates = pawn.health.hediffSet.GetNotMissingParts()
                .Where(p => p.depth == depth)
                .ToList();
            if (candidates.NullOrEmpty()) return;

            BodyPartRecord part = candidates.RandomElement();
            DamageInfo dinfo = new DamageInfo(DamageDefOf.SurgicalCut, Rand.Range(minDamage, maxDamage), 0f, -1f, null, part);
            pawn.TakeDamage(dinfo);
            Messages.Message($"The extraction damaged {pawn.LabelShort}'s {part.Label}.",
                pawn, MessageTypeDefOf.NegativeEvent);
        }
    }
}