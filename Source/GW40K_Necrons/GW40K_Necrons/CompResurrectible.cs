using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Verse.Noise;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;

namespace GW40K_Necrons
{
    public class CompResurrectible : ThingComp
    {
        public CompProperties_Resurrectible Props => (CompProperties_Resurrectible)props;
        
        public bool? canResurrect = null;

        public int ticksToResurrect = 625;

        private Corpse corpse = null;

        //public override void PostPostMake()
        //{
        //    base.PostPostMake();
        //    if (parent is not Corpse c)
        //    {
        //        return;
        //    }
        //    corpse = c;
        //    if (corpse.InnerPawn?.genes == null || !corpse.InnerPawn.genes.HasActiveGene(NecronDefOfs.GW_UD_ResurrectionProtocol))
        //    {
        //        return;
        //    }

        //    if (corpse.InnerPawn?.health.hediffSet.GetBrain() == null)
        //    {
        //        return;
        //    }
        //    canResurrect = true;
        //}

        //public override string CompInspectStringExtra()
        //{
        //    string extra = "";
        //    if (canResurrect.GetValueOrDefault(InitiateCanResurrect()))
        //    {
        //        extra = "will resurrect in " + ticksToResurrect + " ticks";
        //    }
        //    else
        //    {
        //        extra = "wont resurrect";
        //    }
        //    return base.CompInspectStringExtra() + extra;
        //}

        private bool InitiateCanResurrect()
        {

            if (parent is not Corpse c)
            {
                canResurrect = false;
                return false;
            }
            corpse = c;

            if (corpse.InnerPawn == null)
            {
                return false;
            }

            if (corpse.InnerPawn.genes == null || !corpse.InnerPawn.genes.HasActiveGene(NecronDefOfs.GW_UD_ResurrectionProtocol))
            {
                canResurrect = false;
                return false;
            }

            if (corpse.InnerPawn.health.hediffSet.GetBrain() == null)
            {
                canResurrect = false;
                return false;
            }

            if (corpse.InnerPawn.health.summaryHealth.SummaryHealthPercent >= 0.10f)
            {
                canResurrect = false;
                return false;
            }

            canResurrect = true;
            return true;
        }

        public override void CompTickRare()
        {
            base.CompTick();

            if (!canResurrect.GetValueOrDefault(InitiateCanResurrect()))
            {
                return;
            }

            ticksToResurrect -= 250;

            if (ticksToResurrect <= 0)
            {
                // reinitiate canresurrect in case the corpse had been eaten/lost the gene
                InitiateCanResurrect();
                // once tested false, corpse will not be resurrecting on its own anymore
                if (!canResurrect.GetValueOrDefault(false))
                {
                    return;
                }

                Pawn pawn = corpse.InnerPawn;
                //foreach (Hediff h in pawn.health.hediffSet.hediffs)
                //{
                //    if (h.IsLethal)
                //    {
                //        pawn.health.RemoveHediff(h);
                //    }
                //}

                bool selected = Find.Selector.IsSelected(corpse);
                bool spawned = corpse.SpawnedOrAnyParentSpawned;
                IntVec3 loc = corpse.PositionHeld;
                Map map = corpse.MapHeld;
                corpse.InnerPawn = null;
                corpse.Destroy();

                if (spawned && pawn.IsWorldPawn())
                {
                    Find.WorldPawns.RemovePawn(pawn);
                }

                pawn.ForceSetStateToUnspawned();
                PawnComponentsUtility.CreateInitialComponents(pawn);
                pawn.health.Notify_Resurrected(true, 0f);
                if (pawn.Faction != null && pawn.Faction.IsPlayer)
                {
                    pawn.workSettings?.EnableAndInitialize();
                    Find.StoryWatcher.watcherPopAdaptation.Notify_PawnEvent(pawn, PopAdaptationEvent.GainedColonist);
                }

                if (pawn.RaceProps.IsMechanoid && MechRepairUtility.IsMissingWeapon(pawn))
                {
                    MechRepairUtility.GenerateWeapon(pawn);
                }

                if (spawned)
                {
                    GenSpawn.Spawn(pawn, loc, map);
                    Lord lord = pawn.GetLord();
                    if (lord != null)
                    {
                        lord?.Notify_PawnUndowned(pawn);
                    }

                    if (pawn.apparel != null)
                    {
                        List<Apparel> wornApparel = pawn.apparel.WornApparel;
                        for (int i = 0; i < wornApparel.Count; i++)
                        {
                            wornApparel[i].Notify_PawnResurrected(pawn);
                        }
                    }
                    
                    PawnDiedOrDownedThoughtsUtility.RemoveDiedThoughts(pawn);

                    pawn.royalty?.Notify_Resurrected();
                    if (pawn.relations != null)
                    {
                        pawn.relations.hidePawnRelations = false;
                    }

                    if (pawn.guest != null && pawn.guest.IsInteractionEnabled(PrisonerInteractionModeDefOf.Execution))
                    {
                        pawn.guest.SetNoInteraction();
                    }

                    if (selected && pawn != null)
                    {
                        Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
                    }

                    pawn.Drawer.renderer.SetAllGraphicsDirty();
                    pawn.stances.stunner.StunFor(5f.SecondsToTicks(), pawn, addBattleLog: false, showMote: false);

                    pawn.needs.AddOrRemoveNeedsAsAppropriate();
                }
            }
        }
    }
}
