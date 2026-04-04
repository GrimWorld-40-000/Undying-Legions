using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace NecronGeneUtil
{
    public class ThoughtWorker_NecronHateFlesh : ThoughtWorker
    {
        public ThoughtExtension_NecronHateFlesh modExtension => def.GetModExtension<ThoughtExtension_NecronHateFlesh>();
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            if (!otherPawn.RaceProps.Humanlike)
            {
                return ThoughtState.Inactive;
            }
            bool selfHaveHatredGene = p.genes.HasActiveGene(FMJ_DefOf.GW_UD_Hatred) && p.genes.HasActiveGene(modExtension.requireGeneDef);
            bool otherHaveGene = otherPawn.genes.HasActiveGene(FMJ_DefOf.GW_UD_Hatred) && otherPawn.genes.HasActiveGene(modExtension.requireGeneDef);
            if (selfHaveHatredGene)
            {
                if (!otherHaveGene)
                {
                    return ThoughtState.ActiveAtStage(modExtension.stateNotMatch);

                }
                else
                {
                    return ThoughtState.ActiveAtStage(modExtension.stateMatch);
                }
            }
            return ThoughtState.Inactive;
        }
    }
}
