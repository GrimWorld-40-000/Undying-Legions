using HarmonyLib;
using RimWorld;
using System;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof(Toils_Recipe), "FinishRecipeAndStartStoringProduct")]
public static class Patch_FinishRecipeAndStartStoringProduct
{
    public static void Postfix(Toil __result, TargetIndex productIndex)
    {
        __result.AddFinishAction((Action)(() =>
        {
            Pawn actor = __result.actor;
            if (actor == null) return;

            Job curJob = actor.CurJob;
            if (curJob == null || !(curJob.targetA.Thing is Building building)) return;

            // New system: recipe has RecipeExtension_SpawnMech — bind to Command Protocol worker
            RecipeDef recipeDef = curJob.RecipeDef;
            var spawnExt = recipeDef?.GetModExtension<RecipeExtension_SpawnMech>();
            if (spawnExt?.mechKindDef != null)
            {
                PawnKindDef mechKind = spawnExt.mechKindDef;
                LongEventHandler.QueueLongEvent(() =>
                {
                    Pawn mech = PawnGenerator.GeneratePawn(mechKind, actor.Faction);
                    GenSpawn.Spawn(mech, building.Position, building.Map);
                    mech.GetOverseer()?.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, mech);
                    actor.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
                    actor.mechanitor?.AssignPawnControlGroup(mech, null);
                }, "GW40K_SummonMech", false, null);
                return;
            }
        }));
    }
}
