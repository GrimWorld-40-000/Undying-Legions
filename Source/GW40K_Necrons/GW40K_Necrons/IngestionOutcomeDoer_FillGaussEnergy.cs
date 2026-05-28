using RimWorld;
using UnityEngine;
using Verse;

namespace NecronMod
{
    // Fills gauss energy by a fixed number of raw energy units rather than a
    // normalized CurLevel fraction. Converts via: delta = energyAmount / capacity.
    // If the pawn has no capacitor the item does nothing.
    // XML usage:
    //   <li Class="NecronMod.IngestionOutcomeDoer_FillGaussEnergy">
    //     <energyAmount>90</energyAmount>
    //   </li>
    public class IngestionOutcomeDoer_FillGaussEnergy : IngestionOutcomeDoer
    {
        public float energyAmount = 90f;

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            GW40K_Necrons.HediffComp_GaussCapacitor cap = GW40K_Necrons.NechEnergyUtility.GetCapacitorComp(pawn);
            if (cap == null || cap.Props.capacity <= 0f)
                return;

            Need need = pawn.needs?.TryGetNeed(GW40K_Necrons.NecronDefOfs.GW40K_NechEnergy);
            if (need == null)
                return;

            float delta = (energyAmount * ingestedCount) / cap.Props.capacity;
            need.CurLevel = Mathf.Clamp(need.CurLevel + delta, 0f, need.MaxLevel);
        }
    }
}
