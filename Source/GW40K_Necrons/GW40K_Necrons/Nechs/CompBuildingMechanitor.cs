// Decompiled with JetBrains decompiler
// Type: GW40K_Necrons.CompBuildingMechanitor
// Assembly: GW40K_Necrons, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7A7FA5E5-16FF-4234-BCBC-527D2120B282
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\GW40K_Necrons.dll

using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

#nullable disable
namespace GW40K_Necrons;

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
    CompProperties_BuildingMechanitor buildingMechanitor = (CompProperties_BuildingMechanitor) props;
    this.mechKind = buildingMechanitor.mechKind;
    this.controlRadius = buildingMechanitor.controlRadius;
  }

  public override void PostExposeData()
  {
    base.PostExposeData();
    Scribe_References.Look<Pawn>(ref this.mechanitorPawn, "mechanitorPawn");
    Scribe_Collections.Look<Pawn>(ref this.controlledMechs, "controlledMechs", LookMode.Reference);
    Scribe_Defs.Look<PawnKindDef>(ref this.mechKind, "mechKind");
    Scribe_Values.Look<float>(ref this.controlRadius, "controlRadius", 20f);
  }

  public override void PostSpawnSetup(bool respawningAfterLoad)
  {
    base.PostSpawnSetup(respawningAfterLoad);
    if (respawningAfterLoad || this.mechanitorPawn != null)
      return;
    this.spawnQueued = true;
  }

  public override void CompTick()
  {
    base.CompTick();
    if (this.spawnQueued && this.parent.Spawned)
    {
      this.spawnQueued = false;
      LongEventHandler.QueueLongEvent((Action) (() =>
      {
        Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false));
        pawn.health.AddHediff(HediffDef.Named("Mechanitor"));
        GenSpawn.Spawn((Thing) pawn, this.parent.Position, this.parent.Map);
        pawn.Position = this.parent.Position;
        this.mechanitorPawn = pawn;
      }), "GW40K_SpawnMechanitor", false, (Action<Exception>) null);
    }
    if (!this.mechSpawnQueued || !this.parent.Spawned)
      return;
    this.mechSpawnQueued = false;
    LongEventHandler.QueueLongEvent((Action) (() =>
    {
      Pawn pawn = PawnGenerator.GeneratePawn(this.queuedMechKind, Faction.OfPlayer);
      GenSpawn.Spawn((Thing) pawn, this.parent.Position, this.parent.Map);
      pawn.GetOverseer()?.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, pawn);
      if (this.mechanitorPawn != null)
        this.mechanitorPawn.relations.AddDirectRelation(PawnRelationDefOf.Overseer, pawn);
      this.RegisterControlledMech(pawn);
    }), "GW40K_SpawnMech", false, (Action<Exception>) null);
  }

  public void QueueMechSpawn(PawnKindDef kind)
  {
    this.queuedMechKind = kind;
    this.mechSpawnQueued = true;
  }

  public void RegisterControlledMech(Pawn mech)
  {
    if (this.controlledMechs.Contains(mech))
      return;
    this.controlledMechs.Add(mech);
  }

  public bool WithinControlRadius(Pawn mech)
  {
    return (double) mech.Position.DistanceTo(this.parent.Position) <= (double) this.controlRadius;
  }

  public void ForceReturn(Pawn mech) => mech.Position = this.parent.Position;
}
