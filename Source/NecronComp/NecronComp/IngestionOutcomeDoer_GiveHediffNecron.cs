// Decompiled with JetBrains decompiler
// Type: NecronComp.IngestionOutcomeDoer_GiveHediffNecron
// Assembly: NecronComp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 664E73FB-E57C-47E4-B49D-3BC7488C1850
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\NecronComp.dll

using RimWorld;
using System.Collections.Generic;
using Verse;

#nullable disable
namespace NecronComp;

public class IngestionOutcomeDoer_GiveHediffNecron : IngestionOutcomeDoer
{
  public HediffDef hediffDef;
  public HediffDef hediffWhenRaceMatch;
  public float severity = 0.5f;
  public float severityWhenMatch = 0.1f;
  public List<ThingDef> raceException = new List<ThingDef>();

  protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
  {
    if (this.raceException.Contains(pawn.def))
    {
      Hediff hediff = HediffMaker.MakeHediff(this.hediffWhenRaceMatch, pawn);
      float num = (double) this.severity <= 0.0 ? this.hediffDef.initialSeverity : this.severityWhenMatch;
      hediff.Severity = num;
      pawn.health.AddHediff(hediff);
    }
    else
    {
      Hediff hediff = HediffMaker.MakeHediff(this.hediffDef, pawn);
      float num = (double) this.severity <= 0.0 ? this.hediffDef.initialSeverity : this.severity;
      hediff.Severity = num;
      pawn.health.AddHediff(hediff);
    }
  }
}
