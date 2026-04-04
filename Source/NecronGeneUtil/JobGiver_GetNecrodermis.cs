using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NecronGeneUtil
{
    public class JobGiver_GetNecrodermis : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive)
            {
                return 0f;
            }
            if (pawn.needs.TryGetNeed<Need_Necrodermis>() == null)
            {
                return 0f;
            }
            return 9.1f;
        }
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive)
            {
                return null;
            }
            Need_Necrodermis need_necrodermis = pawn.needs?.TryGetNeed<Need_Necrodermis>();
            if (need_necrodermis == null)
            {
                return null;
            }
            if (need_necrodermis.CurLevelPercentage >= 0.25f)
            {
                return null;
            }
            int num = Mathf.FloorToInt((need_necrodermis.MaxLevel - need_necrodermis.CurLevelPercentage));
            if (num > 0.25f)
            {
                Thing necrodermis = GetNecrodermisPack(pawn);
                if (necrodermis != null)
                {
                    Job job = JobMaker.MakeJob(JobDefOf.Ingest, necrodermis);
                    job.count = Mathf.Min(necrodermis.stackCount, num);
                    job.ingestTotalCount = true;
                    return job;
                }
            }
            return null;
        }
        private Thing GetNecrodermisPack(Pawn pawn)
        {
            Thing carriedThing = pawn.carryTracker.CarriedThing;
            if (carriedThing != null && carriedThing.def == FMJ_DefOf.GW40k_Necron_Necrodemis)
            {
                return carriedThing;
            }
            for (int i = 0; i < pawn.inventory.innerContainer.Count; i++)
            {
                if (pawn.inventory.innerContainer[i].def == FMJ_DefOf.GW40k_Necron_Necrodemis)
                {
                    return pawn.inventory.innerContainer[i];
                }
            }
            return GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, pawn.Map.listerThings.ThingsOfDef(FMJ_DefOf.GW40k_Necron_Necrodemis), PathEndMode.OnCell, TraverseParms.For(pawn), 9999f, (Thing t) => pawn.CanReserve(t) && !t.IsForbidden(pawn));
        }
    }
}
