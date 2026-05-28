using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace GW40K_Necrons
{
    public class CompResurrectible : ThingComp
    {
        public CompProperties_Resurrectible Props => (CompProperties_Resurrectible)props;

        public bool? canResurrect = null;
        private Corpse corpse = null;
        private bool triggerNotificationSent = false;
        private ThingDef savedPrimaryWeaponDef;
        private ThingDef savedShieldDef;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref canResurrect, "canResurrect");
            Scribe_Values.Look(ref triggerNotificationSent, "triggerNotificationSent");
            Scribe_Defs.Look(ref savedPrimaryWeaponDef, "savedPrimaryWeaponDef");
            Scribe_Defs.Look(ref savedShieldDef, "savedShieldDef");
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

        /// <summary>
        /// Drafted "destroy corpse" float menu: any Necron corpse where finishing the corpse would prevent or abort
        /// reanimation. Cryptek races omit <see cref="CompNechUncontrolledTimer"/> (comps inherit cleared), so we also
        /// key off the resurrection protocol gene, active protocol hediff, and <see cref="CompResurrectible.canResurrect"/>.
        /// </summary>
        public static bool CorpseIsNecronResurrectionDestroyTarget(Corpse corpse)
        {
            if (corpse?.InnerPawn == null)
                return false;

            Pawn inner = corpse.InnerPawn;

            if (NecronDefOfs.GW40K_Necron_ResurrectionActive != null
                && inner.health?.hediffSet?.GetFirstHediffOfDef(NecronDefOfs.GW40K_Necron_ResurrectionActive) != null)
                return true;

            CompResurrectible comp = corpse.TryGetComp<CompResurrectible>();
            if (comp?.canResurrect == true)
                return true;

            if (inner.TryGetComp<CompNechUncontrolledTimer>() != null)
                return true;

            return CanEnterResurrectionProtocol(inner);
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

            if (savedPrimaryWeaponDef == null || savedShieldDef == null)
                CaptureDroppedGear(pawn, corpse);

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

        // Cooldown prevents a letter per pawn when many enemies fall simultaneously.
        // Alert_NecronResurrecting handles the ongoing right-side warning instead.
        private static int s_lastEnemyLetterTick = -999999;
        private const int EnemyLetterCooldownTicks = 15000; // 6 game-hours

        private static void SendTriggeredNotification(Pawn pawn, Corpse corpse)
        {
            if (IsEnemy(pawn))
            {
                int now = Find.TickManager.TicksGame;
                if (now - s_lastEnemyLetterTick >= EnemyLetterCooldownTicks)
                {
                    s_lastEnemyLetterTick = now;
                    Find.LetterStack.ReceiveLetter(
                        "GW40K_ResurrectionTriggeredEnemyTitle".Translate(),
                        "GW40K_ResurrectionTriggeredEnemyDesc".Translate(),
                        LetterDefOf.ThreatSmall,
                        corpse);
                }
                // Alert_NecronResurrecting shows a persistent right-side warning listing all
                // currently resurrecting enemies — no further per-pawn letters needed.
            }
            else if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Find.LetterStack.ReceiveLetter(
                    "GW40K_ResurrectionTriggeredFriendlyTitle".Translate(),
                    "GW40K_ResurrectionTriggeredFriendlyDesc".Translate(pawn.Named("PAWN")),
                    LetterDefOf.NeutralEvent,
                    corpse);
            }

            if (NecronDefOfs.GW_UD_Concept_ResurrectionProtocol != null)
                PlayerKnowledgeDatabase.KnowledgeDemonstrated(NecronDefOfs.GW_UD_Concept_ResurrectionProtocol, KnowledgeAmount.FrameDisplayed);
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
        private const float ResurrectionDecaySeverityPerDay = 1f;
        private const float NecrodermisFailThreshold = 0.5f;

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
                    bool necrodermisOk = NecrodermisAboveThreshold(pawn);
                    if (h is Hediff_ResurrectionActive ra)
                        ra.isFailing = !necrodermisOk;

                    if (necrodermisOk)
                    {
                        h.Severity = Mathf.Min(1f, h.Severity + (250f / 60000f) * ResurrectionSeverityPerDay);
                        if (h.Severity >= 1f)
                        {
                            CompleteResurrection();
                            return;
                        }
                    }
                    else
                    {
                        h.Severity = Mathf.Max(0.01f, h.Severity - (250f / 60000f) * ResurrectionDecaySeverityPerDay);
                    }
                }
            }

            // Re-validate — abort if conditions changed after protocol started
            InitiateCanResurrect();
            if (canResurrect != true)
                RemoveResurrectionActiveHediff();
        }

        private static bool NecrodermisAboveThreshold(Pawn pawn)
        {
            if (NecronDefOfs.GW_UD_Necrodermis == null) return true;
            Need need = pawn?.needs?.TryGetNeed(NecronDefOfs.GW_UD_Necrodermis);
            if (need == null) return true;
            return need.CurLevelPercentage >= NecrodermisFailThreshold;
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

            // Freshly reanimated Nechs should not inherit stale "uncontrolled for Xs" state from pre-death.
            // If they are still uncontrolled, the timer will naturally restart from zero on subsequent sync ticks.
            pawn.TryGetComp<CompNechUncontrolledTimer>()?.NotifyCommandLinkGained();

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
            TryQueueGearRetrieve(pawn, savedPrimaryWeaponDef, savedShieldDef);

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

        private static void TryQueueGearRetrieve(Pawn pawn, ThingDef weaponDef, ThingDef shieldDef)
        {
            if (weaponDef == null && shieldDef == null)
                return;

            DeferredActions.Schedule(5f.SecondsToTicks() + 30, () =>
            {
                if (!pawn.Spawned || pawn.Dead || pawn.Destroyed) return;

                if (weaponDef != null && pawn.equipment?.Primary == null)
                {
                    // 1. Check own inventory — happens when the weapon was moved from the
                    //    equipment slot to the pawn's inventory during the death/resurrection cycle.
                    //    Transfer directly without a job to avoid the lord interrupting mid-equip.
                    Thing inInv = pawn.inventory?.innerContainer?
                        .FirstOrDefault(t => t.def == weaponDef);
                    if (inInv is ThingWithComps invWeapon)
                    {
                        pawn.inventory.innerContainer.Remove(invWeapon);
                        pawn.equipment.MakeRoomFor(invWeapon);
                        pawn.equipment.AddEquipment(invWeapon);
                    }
                    else
                    {
                        // 2. Search the full map for a dropped/stockpiled instance and
                        //    issue a player-forced equip job to pick it up.
                        Thing weapon = FindDroppedGear(pawn, weaponDef);
                        if (weapon != null)
                            pawn.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Equip, weapon),
                                JobCondition.InterruptForced, null,
                                resumeCurJobAfterwards: false, cancelBusyStances: false);
                    }
                }

                if (shieldDef != null)
                    QueueShieldRetrieve(pawn, shieldDef);
            });
        }

        private static void QueueShieldRetrieve(Pawn pawn, ThingDef shieldDef)
        {
            DeferredActions.Schedule(120, () =>
            {
                if (!pawn.Spawned || pawn.Dead || pawn.Destroyed) return;
                if (pawn.apparel?.WornApparel.Any(a => a.def == shieldDef) == true) return;

                Thing shield = FindDroppedGear(pawn, shieldDef);
                if (shield is Apparel ap && pawn.apparel?.CanWearWithoutDroppingAnything(ap.def) == true)
                    pawn.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Wear, shield),
                        JobCondition.InterruptForced, null,
                        resumeCurJobAfterwards: false, cancelBusyStances: false);
            });
        }

        /// <summary>Remember primary weapon and necron shield at protocol start (worn or dropped beside corpse).</summary>
        private void CaptureDroppedGear(Pawn pawn, Corpse c)
        {
            if (savedPrimaryWeaponDef == null)
            {
                ThingWithComps primary = pawn.equipment?.Primary;
                if (primary != null)
                    savedPrimaryWeaponDef = primary.def;
                else
                    savedPrimaryWeaponDef = FindClosestDroppedDefNear(c, def => def.IsWeapon);
            }

            if (savedShieldDef == null)
            {
                Apparel wornShield = pawn.apparel?.WornApparel
                    .FirstOrDefault(a => IsNecronShieldDef(a.def));
                if (wornShield != null)
                    savedShieldDef = wornShield.def;
                else
                    savedShieldDef = FindClosestDroppedDefNear(c, IsNecronShieldDef);
            }
        }

        private static bool IsNecronShieldDef(ThingDef def) =>
            def != null && (def.defName == "GM40k_Necron_Shield"
                || def.apparel?.tags?.Contains("GW_Necron_Shield") == true);

        private static ThingDef FindClosestDroppedDefNear(Corpse c, Predicate<ThingDef> matcher)
        {
            Map map = c.MapHeld ?? c.Map;
            if (map == null || matcher == null)
                return null;

            IntVec3 origin = c.PositionHeld;
            Thing best = null;
            float bestDist = float.MaxValue;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(origin, 2f, useCenter: true))
            {
                if (!cell.InBounds(map))
                    continue;

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    if (t?.def == null || !matcher(t.def))
                        continue;

                    float dist = cell.DistanceToSquared(origin);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = t;
                    }
                }
            }

            return best?.def;
        }

        private static Thing FindDroppedGear(Pawn pawn, ThingDef gearDef)
        {
            if (gearDef == null || !pawn.Spawned || pawn.Map == null)
                return null;

            // Use the def-indexed lister for an efficient full-map scan rather than a radius search.
            // Matches the same pattern used by JobGiver_GetGaussCore — O(n) over matching Things only,
            // not O(cells). This handles weapons that were stockpiled far from the resurrection site.
            return GenClosest.ClosestThing_Global_Reachable(
                pawn.Position,
                pawn.Map,
                pawn.Map.listerThings.ThingsOfDef(gearDef),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                t => t.Spawned && !t.IsForbidden(pawn));
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

    // ── Float menu: drafted pawn destroys Necron corpse to prevent resurrection ──

    /// <summary>
    /// Adds "Destroy [corpse] (prevent resurrection)" when a drafted colonist
    /// right-clicks a Necron corpse. RimWorld 1.6 auto-discovers
    /// FloatMenuOptionProvider subclasses via reflection — no Harmony patch needed.
    /// Target corpses: <see cref="CompResurrectible.CorpseIsNecronResurrectionDestroyTarget"/> (Cryptek, Warriors, etc.).
    /// Uses <see cref="FloatMenuContext.ClickedCell"/> (not <c>UI.MouseCell</c>) and avoids
    /// <see cref="FloatMenuUtility.DecoratePrioritizedTask"/> so the option does not gray out when the cursor moves onto the menu.
    /// </summary>
    public class NecronCorpseDestroyMenuProvider : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => false;
        protected override bool Multiselect => false;

        // Never auto-take this job on a direct click/attack order — only surface it in the right-click menu.
        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context) => null;

        public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
        {
            Pawn pawn = context.FirstSelectedPawn;
            if (pawn?.Faction != Faction.OfPlayer || pawn.Map == null)
                yield break;

            Map map = context.map;
            if (map == null || pawn.Map != map)
                yield break;

            // Stable click cell from the float-menu context (MouseCell drifts as soon as the menu opens).
            IntVec3 cell = context.ClickedCell;
            if (!cell.InBounds(map))
                yield break;

            JobDef destroyJob = NecronDefOfs.GW40K_Job_DestroyNecronCorpse;
            if (destroyJob == null)
                yield break;

            // Thing grid includes forbidden corpses; ClickedThings may omit them.
            foreach (Thing thing in map.thingGrid.ThingsListAt(cell))
            {
                if (thing is not Corpse corpse)
                    continue;
                if (!CompResurrectible.CorpseIsNecronResurrectionDestroyTarget(corpse))
                    continue;

                string label = "GW40K_DestroyNecronCorpse".Translate(corpse.InnerPawn.LabelShortCap);

                if (pawn.WorkTagIsDisabled(WorkTags.Violent))
                {
                    yield return new FloatMenuOption(label + " (" + "IsIncapableOfViolenceLower".Translate(pawn.LabelShort, pawn) + ")", null);
                    continue;
                }

                if (!pawn.CanReach(corpse, PathEndMode.Touch, Danger.Deadly))
                {
                    yield return new FloatMenuOption(label + " (" + "NoPath".Translate() + ")", null);
                    continue;
                }

                if (!pawn.CanReserve(corpse))
                {
                    yield return new FloatMenuOption(label + " (" + "Reserved".Translate() + ")", null);
                    continue;
                }

                Corpse corpseCapture = corpse;
                // Do not use FloatMenuUtility.DecoratePrioritizedTask: it sets revalidateClickTarget to the corpse,
                // so moving the cursor onto the menu entry disables the option (cursor no longer "on" the corpse).
                // Unforbid the corpse on click so a player-issued destroy order is never silently blocked
                // by a forbidden flag (same pattern as gauss core manual siphon).
                yield return new FloatMenuOption(label, () =>
                {
                    if (corpseCapture.IsForbidden(pawn))
                        corpseCapture.SetForbidden(false, false);
                    Job job = JobMaker.MakeJob(destroyJob, corpseCapture);
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }
        }
    }
}
