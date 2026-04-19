using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace GW40K_Necrons
{
    public class CompResurrectible : ThingComp
    {
        public CompProperties_Resurrectible Props => (CompProperties_Resurrectible)props;

        public bool? canResurrect = null;
        private Corpse corpse = null;
        private bool triggerNotificationSent = false;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref canResurrect, "canResurrect");
            Scribe_Values.Look(ref triggerNotificationSent, "triggerNotificationSent");
            Scribe_References.Look(ref corpse, "corpse");
        }

        // ── Core checks ───────────────────────────────────────────────────────

        /// <summary>
        /// Resurrection is possible when AT LEAST ONE core is intact.
        /// Both cores destroyed = permanent death (no resurrection).
        /// Falls back to brain check for non-Necron bodies that lack these parts.
        /// </summary>
        private static bool HasVitalCores(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || pawn.RaceProps?.body == null)
                return false;

            BodyPartDef mindCoreDef    = DefDatabase<BodyPartDef>.GetNamedSilentFail("GW40K_Necron_MindCore");
            BodyPartDef centralCoreDef = DefDatabase<BodyPartDef>.GetNamedSilentFail("GW40K_Necron_CentralCore");

            if (mindCoreDef == null || centralCoreDef == null)
                return pawn.health.hediffSet.GetBrain() != null;

            BodyPartRecord mindCore    = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == mindCoreDef);
            BodyPartRecord centralCore = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == centralCoreDef);

            if (mindCore == null || centralCore == null)
                return pawn.health.hediffSet.GetBrain() != null;

            bool mindIntact    = !pawn.health.hediffSet.PartIsMissing(mindCore);
            bool centralIntact = !pawn.health.hediffSet.PartIsMissing(centralCore);
            return mindIntact || centralIntact;
        }

        /// <summary>
        /// Returns true if either Necron core has been destroyed (missing).
        /// Used by ShouldBeDead patch to force death when the destroyed-core trigger fires.
        /// </summary>
        public static bool IsAnyCoreDestroyed(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || pawn.RaceProps?.body == null)
                return false;

            BodyPartDef mindCoreDef    = DefDatabase<BodyPartDef>.GetNamedSilentFail("GW40K_Necron_MindCore");
            BodyPartDef centralCoreDef = DefDatabase<BodyPartDef>.GetNamedSilentFail("GW40K_Necron_CentralCore");

            if (mindCoreDef == null || centralCoreDef == null)
                return false;

            BodyPartRecord mindCore    = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == mindCoreDef);
            BodyPartRecord centralCore = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == centralCoreDef);

            if (mindCore == null || centralCore == null)
                return false;

            return pawn.health.hediffSet.PartIsMissing(mindCore)
                || pawn.health.hediffSet.PartIsMissing(centralCore);
        }

        /// <summary>
        /// Returns true if BOTH cores have been destroyed (permanent death; no protocol).
        /// </summary>
        public static bool IsBothCoresDestroyed(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || pawn.RaceProps?.body == null)
                return false;

            BodyPartDef mindCoreDef    = DefDatabase<BodyPartDef>.GetNamedSilentFail("GW40K_Necron_MindCore");
            BodyPartDef centralCoreDef = DefDatabase<BodyPartDef>.GetNamedSilentFail("GW40K_Necron_CentralCore");

            if (mindCoreDef == null || centralCoreDef == null)
                return false;

            BodyPartRecord mindCore    = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == mindCoreDef);
            BodyPartRecord centralCore = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == centralCoreDef);

            if (mindCore == null || centralCore == null)
                return false;

            return pawn.health.hediffSet.PartIsMissing(mindCore)
                && pawn.health.hediffSet.PartIsMissing(centralCore);
        }

        /// <summary>
        /// Same eligibility as <see cref="InitiateCanResurrect"/> for the inner pawn (living or corpse).
        /// Used when deciding whether to allow vanilla lethal death so a corpse with CompResurrectible can form.
        /// </summary>
        public static bool CanEnterResurrectionProtocol(Pawn pawn)
        {
            if (pawn?.genes == null || !pawn.genes.HasActiveGene(NecronDefOfs.GW_UD_ResurrectionProtocol))
                return false;
            if (pawn.genes.GetGene(NecronDefOfs.GW_UD_ResurrectionProtocol)
                    .def.GetModExtension<GeneExtension_Resurrection>() == null)
                return false;
            if (NecronDefOfs.GW40K_ReanimationCooldown != null
                && pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_ReanimationCooldown) != null)
                return false;
            if (IsBothCoresDestroyed(pawn))
                return false;
            if (!HasVitalCores(pawn))
                return false;
            bool coreDied = IsAnyCoreDestroyed(pawn);
            if (!coreDied && pawn.health.summaryHealth.SummaryHealthPercent >= 0.10f)
                return false;
            return true;
        }

        private static bool IsEnemy(Pawn pawn) =>
            pawn?.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer);

        private static float GetCoreSize(Pawn pawn)
        {
            if (pawn?.def == null) return 1f;
            NonOrganicPawn neo = pawn.def.GetModExtension<NonOrganicPawn>();
            if (neo != null) return neo.coreSize > 0f ? neo.coreSize : 1f;
            NecronMechExtension mech = pawn.def.GetModExtension<NecronMechExtension>();
            if (mech != null) return mech.coreSize > 0f ? mech.coreSize : 1f;
            return 1f;
        }

        // ── Initiation ────────────────────────────────────────────────────────

        private bool InitiateCanResurrect()
        {
            if (parent is not Corpse c)
            {
                canResurrect = false;
                return false;
            }
            corpse = c;

            if (corpse.InnerPawn == null)
                return false;

            Pawn pawn = corpse.InnerPawn;

            if (pawn.genes == null || !pawn.genes.HasActiveGene(NecronDefOfs.GW_UD_ResurrectionProtocol)
                || pawn.genes.GetGene(NecronDefOfs.GW_UD_ResurrectionProtocol).def.GetModExtension<GeneExtension_Resurrection>() == null)
            {
                canResurrect = false;
                return false;
            }

            // Cooldown active → cannot reanimate again yet
            if (NecronDefOfs.GW40K_ReanimationCooldown != null
                && pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_ReanimationCooldown) != null)
            {
                canResurrect = false;
                return false;
            }

            // Both cores destroyed → permanent death, no reanimation
            if (IsBothCoresDestroyed(pawn))
            {
                canResurrect = false;
                return false;
            }

            if (!HasVitalCores(pawn))
            {
                canResurrect = false;
                return false;
            }

            // Skip 10% health threshold when core destruction was the cause of death
            bool coreDied = IsAnyCoreDestroyed(pawn);
            if (!coreDied && pawn.health.summaryHealth.SummaryHealthPercent >= 0.10f)
            {
                canResurrect = false;
                return false;
            }

            canResurrect = true;

            // Apply whole-body hediff so the health tab shows the protocol is active
            if (NecronDefOfs.GW40K_Necron_ResurrectionActive != null
                && !pawn.health.hediffSet.HasHediff(NecronDefOfs.GW40K_Necron_ResurrectionActive))
            {
                pawn.health.AddHediff(NecronDefOfs.GW40K_Necron_ResurrectionActive);
            }

            // Send "triggered" notification once
            if (!triggerNotificationSent && corpse.SpawnedOrAnyParentSpawned)
            {
                triggerNotificationSent = true;
                SendTriggeredNotification(pawn, corpse);
            }

            return true;
        }

        private static void SendTriggeredNotification(Pawn pawn, Corpse corpse)
        {
            if (IsEnemy(pawn))
            {
                Find.LetterStack.ReceiveLetter(
                    "GW40K_ResurrectionTriggeredEnemyTitle".Translate(),
                    "GW40K_ResurrectionTriggeredEnemyDesc".Translate(),
                    LetterDefOf.ThreatSmall,
                    corpse);
            }
            else if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Find.LetterStack.ReceiveLetter(
                    "GW40K_ResurrectionTriggeredFriendlyTitle".Translate(),
                    "GW40K_ResurrectionTriggeredFriendlyDesc".Translate(pawn.Named("PAWN")),
                    LetterDefOf.NeutralEvent,
                    corpse);
            }
        }

        // ── Tick ─────────────────────────────────────────────────────────────

        private void RemoveResurrectionActiveHediff()
        {
            Pawn pawn = (parent as Corpse)?.InnerPawn;
            if (pawn == null || NecronDefOfs.GW40K_Necron_ResurrectionActive == null) return;
            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_Necron_ResurrectionActive);
            if (h != null) pawn.health.RemoveHediff(h);
        }

        // SeverityPerDay comp doesn't tick on corpse inner pawns — drive it manually here.
        private const float ResurrectionSeverityPerDay = 4f;

        public override void CompTickRare()
        {
            base.CompTick();

            if (!canResurrect.HasValue)
            {
                InitiateCanResurrect();
                return;
            }

            if (canResurrect != true)
                return;

            // Advance hediff severity (250 ticks per CompTickRare, 60000 ticks per day).
            Pawn pawn = corpse?.InnerPawn;
            if (pawn != null && NecronDefOfs.GW40K_Necron_ResurrectionActive != null)
            {
                Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_Necron_ResurrectionActive);
                if (h != null)
                {
                    h.Severity = Mathf.Min(1f, h.Severity + (250f / 60000f) * ResurrectionSeverityPerDay);
                    if (h.Severity >= 1f)
                    {
                        CompleteResurrection();
                        return;
                    }
                }
            }

            // Re-validate — abort if conditions changed after protocol started
            InitiateCanResurrect();
            if (canResurrect != true)
                RemoveResurrectionActiveHediff();
        }

        internal void CompleteResurrection()
        {
            Pawn pawn    = corpse.InnerPawn;
            bool enemy   = IsEnemy(pawn);
            bool selected = Find.Selector.IsSelected(corpse);
            bool spawned  = corpse.SpawnedOrAnyParentSpawned;
            IntVec3 loc   = corpse.PositionHeld;
            Map map       = corpse.MapHeld;

            RemoveResurrectionActiveHediff();

            corpse.InnerPawn = null;
            corpse.Destroy();

            if (spawned && Find.WorldPawns.Contains(pawn))
                Find.WorldPawns.RemovePawn(pawn);

            pawn.ForceSetStateToUnspawned();
            PawnComponentsUtility.CreateInitialComponents(pawn);
            pawn.health.Notify_Resurrected(true, 0f);

            // Apply 30-day reanimation cooldown so the protocol can't fire again immediately
            if (NecronDefOfs.GW40K_ReanimationCooldown != null)
                HealthUtility.AdjustSeverity(pawn, NecronDefOfs.GW40K_ReanimationCooldown, 1.0f);

            if (pawn.Faction != null && pawn.Faction.IsPlayer)
            {
                pawn.workSettings?.EnableAndInitialize();
                Find.StoryWatcher.watcherPopAdaptation.Notify_PawnEvent(pawn, PopAdaptationEvent.GainedColonist);
            }

            if (pawn.RaceProps.IsMechanoid && MechRepairUtility.IsMissingWeapon(pawn))
                MechRepairUtility.GenerateWeapon(pawn);

            if (!spawned)
                return;

            GenSpawn.Spawn(pawn, loc, map);

            // Re-attach enemy to an active raid lord so they continue fighting
            if (enemy)
            {
                Lord lord = pawn.GetLord();
                if (lord != null)
                {
                    lord.Notify_PawnUndowned(pawn);
                }
                else
                {
                    Lord hostileLord = map.lordManager.lords
                        .FirstOrDefault(l => l.faction != null && l.faction.HostileTo(Faction.OfPlayer));
                    hostileLord?.AddPawn(pawn);
                }
            }
            else
            {
                Lord lord = pawn.GetLord();
                lord?.Notify_PawnUndowned(pawn);
            }

            if (pawn.apparel != null)
            {
                List<Apparel> worn = pawn.apparel.WornApparel;
                for (int i = 0; i < worn.Count; i++)
                    worn[i].Notify_PawnResurrected(pawn);
            }

            PawnDiedOrDownedThoughtsUtility.RemoveDiedThoughts(pawn);
            pawn.royalty?.Notify_Resurrected();

            if (pawn.relations != null)
                pawn.relations.hidePawnRelations = false;

            if (pawn.guest != null && pawn.guest.IsInteractionEnabled(PrisonerInteractionModeDefOf.Execution))
                pawn.guest.SetNoInteraction();

            if (selected)
                Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);

            pawn.Drawer.renderer.SetAllGraphicsDirty();
            pawn.stances.stunner.StunFor(5f.SecondsToTicks(), pawn, addBattleLog: false, showMote: false);
            pawn.needs.AddOrRemoveNeedsAsAppropriate();

            // Completion notification
            SendCompletedNotification(pawn, enemy);
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (canResurrect != true || parent is not Corpse c || !c.Spawned)
                return;

            float progress = 0f;
            if (c.InnerPawn != null && NecronDefOfs.GW40K_Necron_ResurrectionActive != null)
                progress = c.InnerPawn.health.hediffSet
                    .GetFirstHediffOfDef(NecronDefOfs.GW40K_Necron_ResurrectionActive)?.Severity ?? 0f;

            ResurrectionProtocolVisuals.DrawOverCorpse(c, progress);
        }

        private static void SendCompletedNotification(Pawn pawn, bool enemy)
        {
            if (enemy)
            {
                Find.LetterStack.ReceiveLetter(
                    "GW40K_ResurrectionCompletedEnemyTitle".Translate(),
                    "GW40K_ResurrectionCompletedEnemyDesc".Translate(),
                    LetterDefOf.ThreatBig,
                    pawn);
            }
            else if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Find.LetterStack.ReceiveLetter(
                    "GW40K_ResurrectionCompletedFriendlyTitle".Translate(),
                    "GW40K_ResurrectionCompletedFriendlyDesc".Translate(pawn.Named("PAWN")),
                    LetterDefOf.PositiveEvent,
                    pawn);
            }
        }
    }
}
