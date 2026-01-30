using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace GW40K_Necrons
{
    public class CompProperties_BuildingMechanitor : CompProperties
    {
        public PawnKindDef mechKind;
        public float controlRadius = 20f;

        public CompProperties_BuildingMechanitor()
        {
            this.compClass = typeof(CompBuildingMechanitor);
        }
    }

    public class CompBuildingMechanitor : ThingComp
    {
        public Pawn mechanitorPawn;
        public List<Pawn> controlledMechs = new List<Pawn>();
        public float controlRadius = 20f;
        public PawnKindDef mechKind;

        private bool spawnQueued;

        private PawnKindDef queuedMechKind;
        private bool mechSpawnQueued;

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            var p = (CompProperties_BuildingMechanitor)props;
            mechKind = p.mechKind;
            controlRadius = p.controlRadius;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref mechanitorPawn, "mechanitorPawn");
            Scribe_Collections.Look(ref controlledMechs, "controlledMechs", LookMode.Reference);
            Scribe_Defs.Look(ref mechKind, "mechKind");
            Scribe_Values.Look(ref controlRadius, "controlRadius", 20f);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (!respawningAfterLoad && mechanitorPawn == null)
                spawnQueued = true;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (spawnQueued && parent.Spawned)
            {
                spawnQueued = false;

                LongEventHandler.QueueLongEvent(() =>
                {
                    var req = new PawnGenerationRequest(
                        PawnKindDefOf.Colonist,
                        Faction.OfPlayer,
                        forceGenerateNewPawn: true,
                        allowDead: false,
                        allowDowned: false,
                        canGeneratePawnRelations: false,
                        mustBeCapableOfViolence: false
                    );

                    var pawn = PawnGenerator.GeneratePawn(req);
                    pawn.health.AddHediff(HediffDef.Named("Mechanitor"));
                    GenSpawn.Spawn(pawn, parent.Position, parent.Map);
                    pawn.Position = parent.Position;
                    mechanitorPawn = pawn;

                }, "GW40K_SpawnMechanitor", false, null);
            }

            if (mechSpawnQueued && parent.Spawned)
            {
                mechSpawnQueued = false;

                LongEventHandler.QueueLongEvent(() =>
                {
                    var mech = PawnGenerator.GeneratePawn(queuedMechKind, Faction.OfPlayer);
                    GenSpawn.Spawn(mech, parent.Position, parent.Map);

                    // bind mech to the hidden mechanitor pawn in the building
                    var currentOverseer = mech.GetOverseer();
                    if (currentOverseer != null)
                    {
                        currentOverseer.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, mech);
                    }
                    if (mechanitorPawn != null)
                    {
                        mechanitorPawn.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
                    }

                    RegisterControlledMech(mech);

                }, "GW40K_SpawnMech", false, null);
            }
        }

        public void QueueMechSpawn(PawnKindDef kind)
        {
            queuedMechKind = kind;
            mechSpawnQueued = true;
        }

        public void RegisterControlledMech(Pawn mech)
        {
            if (!controlledMechs.Contains(mech))
                controlledMechs.Add(mech);
        }

        public bool WithinControlRadius(Pawn mech)
        {
            return mech.Position.DistanceTo(parent.Position) <= controlRadius;
        }

        public void ForceReturn(Pawn mech)
        {
            mech.Position = parent.Position;
        }
    }

    
    public class RecipeWorker_SpawnAndBindMech : RecipeWorker
    {
        // intentionally empty 
    }

    
    [HarmonyPatch(typeof(Toils_Recipe), "FinishRecipeAndStartStoringProduct")]
    public static class Patch_FinishRecipeAndStartStoringProduct
    {
        public static void Postfix(Toil __result, TargetIndex billGiverInd)
        {
         
            var toil = __result;
            toil.AddFinishAction(() =>
            {
                var pawn = toil.actor;
                if (pawn == null)
                    return;

                var job = pawn.CurJob;
                if (job == null)
                    return;

                var building = job.targetA.Thing as Building;
                if (building == null)
                    return;

                var comp = building.GetComp<CompBuildingMechanitor>();
                if (comp == null)
                    return;

                var mechKind = comp.mechKind;
                if (mechKind == null)
                    return;

                comp.QueueMechSpawn(mechKind);
            });
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "TryEnterNextPathCell")]
    public static class Patch_PathFollower_TryEnterNextPathCell
    {
        static readonly AccessTools.FieldRef<Pawn_PathFollower, Pawn> pawnRef =
            AccessTools.FieldRefAccess<Pawn_PathFollower, Pawn>("pawn");

        public static void Postfix(Pawn_PathFollower __instance)
        {
            Pawn mech = pawnRef(__instance);
            if (mech == null || mech.Map == null || !mech.RaceProps.IsMechanoid)
                return;

            var things = mech.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
            if (things == null)
                return;

            for (int i = 0; i < things.Count; i++)
            {
                var b = things[i];
                var comp = b.TryGetComp<CompBuildingMechanitor>();
                if (comp == null)
                    continue;

                if (!comp.controlledMechs.Contains(mech))
                    continue;

                if (!comp.WithinControlRadius(mech))
                {
                    comp.ForceReturn(mech);
                    return;
                }
            }
        }
    }
}
