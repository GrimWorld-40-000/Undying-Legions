using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace NecronMod
{
    // Administers litho-phagic acid to a pawn.
    // Removes the Necron_NecrodermisGrowth hediff if present, then applies
    // burn damage to random body parts scaled to how much necrodermis was present —
    // more metal means a larger exothermic reaction and worse burns.
    public class RecipeWorker_LithoPhagicAcid : RecipeWorker
    {
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("Necron_NecrodermisGrowth"));

            if (hediff == null)
            {
                Messages.Message(
                    $"No necrodermis detected in {pawn.LabelShort}. The acid had no effect.",
                    pawn, MessageTypeDefOf.CautionInput);
                return;
            }

            float severity = hediff.Severity;
            pawn.health.RemoveHediff(hediff);

            // Burns scale with severity: higher severity = more metal reacting =
            // more superheated chlorine gas and corrosive thermal energy released.
            int burnCount  = Mathf.RoundToInt(severity * 4f) + 1; // 1 at min, 5 at max
            float maxDamage = Mathf.Lerp(4f, 18f, severity);

            var candidates = pawn.health.hediffSet.GetNotMissingParts().ToList();
            for (int i = 0; i < burnCount && candidates.Count > 0; i++)
            {
                BodyPartRecord burnPart = candidates.RandomElement();
                float damage = Rand.Range(maxDamage * 0.5f, maxDamage);
                DamageInfo dinfo = new DamageInfo(DamageDefOf.Burn, damage, 0f, -1f, null, burnPart);
                pawn.TakeDamage(dinfo);
            }

            Messages.Message(
                $"The litho-phagic acid neutralized the necrodermis in {pawn.LabelShort}. " +
                $"The exothermic reaction caused {burnCount} chemical burn(s).",
                pawn, MessageTypeDefOf.NeutralEvent);
        }

        // Only show this recipe on pawns that actually have the necrodermis hediff.
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (thing is Pawn pawn)
                return pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("Necron_NecrodermisGrowth")) != null;
            return false;
        }
    }
}
