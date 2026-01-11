// Decompiled with JetBrains decompiler
// Type: NecronComp.IncidentWorker_NecrodemisInfection
// Assembly: NecronComp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 664E73FB-E57C-47E4-B49D-3BC7488C1850
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\NecronComp.dll

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

#nullable disable
namespace NecronComp;

public class IncidentWorker_NecrodemisInfection : IncidentWorker
{
  public ModExtension_NecrodemisInfection modExtension
  {
    get => this.def.GetModExtension<ModExtension_NecrodemisInfection>();
  }

  protected override bool CanFireNowSub(IncidentParms parms)
  {
    return base.CanFireNowSub(parms) && this.AnyValidMineable((Map) parms.target);
  }

  protected override bool TryExecuteWorker(IncidentParms parms)
  {
    Map map = (Map) parms.target;
    if (!this.AnyValidMineable(map))
      return false;
    List<Thing> thingList1 = new List<Thing>(map.spawnedThings.Where<Thing>((Func<Thing, bool>) (x => this.IsMineable(x, map))));
    List<Thing> thingList2 = new List<Thing>();
    int num = 0;
    foreach (Thing thing in thingList1)
    {
      if (thing is Mineable)
      {
        if (!this.modExtension.excludedThings.Contains(thing.def) && thing.def != this.modExtension.replaceWith)
        {
          thing?.Destroy();
          Thing newThing = ThingMaker.MakeThing(this.modExtension.replaceWith);
          GenSpawn.Spawn(newThing, thing.Position, map);
          thingList2.Add(newThing);
          ++num;
        }
        else
          continue;
      }
      if (num >= this.modExtension.countPerTrigger)
        break;
    }
    Messages.Message((string) "NecrodemisInfection".Translate(), MessageTypeDefOf.NegativeEvent);
    string label = "Necrodemis Spread";
    StringBuilder sb = new StringBuilder();
    sb.Append(label);
    sb.AppendInNewLine("some ore on the map has been infected by Necrodemis");
    Find.LetterStack.ReceiveLetter((TaggedString) label, (TaggedString) sb.ToString(), LetterDefOf.NeutralEvent, (LookTargets) thingList2);
    return true;
  }

  public bool AnyValidMineable(Map map)
  {
    return map.spawnedThings.Where<Thing>((Func<Thing, bool>) (x => this.IsMineable(x, map))).Any<Thing>();
  }

  public bool IsMineable(Thing t, Map map)
  {
    return t is Mineable mineable && mineable.def.building.isResourceRock;
  }
}
