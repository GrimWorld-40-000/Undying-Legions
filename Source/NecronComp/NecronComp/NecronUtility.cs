// Decompiled with JetBrains decompiler
// Type: NecronComp.NecronUtility
// Assembly: NecronComp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 664E73FB-E57C-47E4-B49D-3BC7488C1850
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\NecronComp.dll

using System.Collections.Generic;
using Verse;

#nullable disable
namespace NecronComp;

public static class NecronUtility
{
  public static List<Pawn> GetNearbyPawnFriendAndFoe(this IntVec3 center, Map map, float radius)
  {
    List<Pawn> pawnFriendAndFoe = new List<Pawn>();
    float num = radius * radius;
    foreach (Pawn pawn in (IEnumerable<Pawn>) map.mapPawns.AllPawnsSpawned)
    {
      if (pawn.Spawned && !pawn.Dead && (double) pawn.Position.DistanceToSquared(center) <= (double) num)
        pawnFriendAndFoe.Add(pawn);
    }
    return pawnFriendAndFoe;
  }
}
