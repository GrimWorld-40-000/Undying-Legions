// Decompiled with JetBrains decompiler
// Type: NecronComp.NecronStartPatch
// Assembly: NecronComp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 664E73FB-E57C-47E4-B49D-3BC7488C1850
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\NecronComp.dll

using HarmonyLib;
using Verse;

#nullable disable
namespace NecronComp;

[StaticConstructorOnStartup]
public static class NecronStartPatch
{
  static NecronStartPatch() => new Harmony("FarmerJoe.NecronPatch").PatchAll();
}
