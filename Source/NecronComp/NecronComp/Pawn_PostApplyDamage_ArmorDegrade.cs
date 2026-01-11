// Decompiled with JetBrains decompiler
// Type: NecronComp.Pawn_PostApplyDamage_ArmorDegrade
// Assembly: NecronComp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 664E73FB-E57C-47E4-B49D-3BC7488C1850
// Assembly location: C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Undying-Legions\Assemblies\NecronComp.dll

using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace NecronComp;

[HarmonyPatch(typeof (Pawn))]
[HarmonyPatch("PostApplyDamage")]
public class Pawn_PostApplyDamage_ArmorDegrade
{
  private static void Postfix(ref DamageInfo dinfo, ref float totalDamageDealt, Pawn __instance)
  {
    pawn = (Pawn) null;
    if ((__instance == null || dinfo.Instigator == null ? 1 : (!(dinfo.Instigator is Pawn pawn) ? 1 : 0)) != 0 || pawn.equipment.Primary == null)
      return;
    ModExtension_BulletArmorDegrade modExtension = pawn.equipment.Primary.def.GetModExtension<ModExtension_BulletArmorDegrade>();
    if (modExtension == null || !__instance.RaceProps.Humanlike || __instance.apparel == null || __instance.apparel.WornApparel.NullOrEmpty<Apparel>())
      return;
    foreach (Apparel apparel in __instance.apparel.WornApparel)
    {
      float num = (float) apparel.MaxHitPoints * Rand.Range(0.01f, 0.05f);
      apparel.HitPoints -= Mathf.RoundToInt((float) modExtension.hpDeductAmount + num);
      if (apparel.HitPoints <= 0)
        apparel.Destroy(DestroyMode.Vanish);
    }
  }
}
