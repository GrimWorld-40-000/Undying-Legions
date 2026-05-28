using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace GW40K_Necrons;

public class CompProperties_GaussCapacitorApparel : CompProperties
{
    public CompProperties_GaussCapacitorApparel() => compClass = typeof(Comp_GaussCapacitorApparel);
}

public class Comp_GaussCapacitorApparel : ThingComp { }

// ── Draw a small green indicator square on the back of north-facing wearers ───

[StaticConstructorOnStartup]
[HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
[HarmonyPatch(new[] { typeof(Vector3), typeof(Rot4?), typeof(bool) })]
static class HarmonyPatch_GaussCapacitorPack_NorthIndicator
{
    private static readonly Material IndicatorMat;

    static HarmonyPatch_GaussCapacitorPack_NorthIndicator()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        for (int x = 0; x < 4; x++)
        for (int y = 0; y < 4; y++)
            tex.SetPixel(x, y, Color.white);
        tex.Apply();
        IndicatorMat = new Material(ShaderDatabase.Transparent)
        {
            mainTexture = tex,
            color = new Color(0.1f, 0.9f, 0.1f, 0.95f)
        };
    }

    public static void Postfix(PawnRenderer __instance, Vector3 drawLoc)
    {
        Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
        if (pawn == null) return;
        if (pawn.Rotation != Rot4.North) return;
        if (pawn.apparel?.WornApparel == null) return;
        if (!pawn.apparel.WornApparel.Any(a => a.GetComp<Comp_GaussCapacitorApparel>() != null)) return;
        if (!GaussWeaponUtil.HasEquippedGaussWeapon(pawn)) return;

        Vector3 pos = drawLoc;
        pos.y += Altitudes.AltIncVect.y * 3f;
        pos.z -= 0.18f;
        Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(0.18f, 1f, 0.26f));
        Graphics.DrawMesh(MeshPool.plane10, matrix, IndicatorMat, 0);
    }
}

// ── Add capacitor hediff when the pack is worn ────────────────────────────────

[HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
static class HarmonyPatch_GaussCapacitorApparel_Wear
{
    static void Postfix(Pawn_ApparelTracker __instance, Apparel newApparel)
    {
        if (newApparel?.GetComp<Comp_GaussCapacitorApparel>() == null) return;

        Pawn pawn = __instance.pawn;
        if (pawn == null) return;
        if (NechEnergyUtility.IsNecronPawn(pawn)) return; // Necrons already have a capacitor from spawn
        if (NechEnergyUtility.GetCapacitorComp(pawn) != null) return;

        HediffDef capacitorDef = NechEnergyUtility.CapacitorSmallDef;
        if (capacitorDef == null) return;

        pawn.health.AddHediff(capacitorDef);
        pawn.needs?.AddOrRemoveNeedsAsAppropriate();
        Need energy = pawn.needs?.TryGetNeed(NecronDefOfs.GW40K_NechEnergy);
        if (energy != null) energy.CurLevel = 1f;
    }
}

// ── Remove capacitor hediff when the pack is unequipped ───────────────────────

[HarmonyPatch(typeof(Pawn_ApparelTracker), "Remove")]
static class HarmonyPatch_GaussCapacitorApparel_Remove
{
    static void Postfix(Pawn_ApparelTracker __instance, Apparel ap)
    {
        if (ap?.GetComp<Comp_GaussCapacitorApparel>() == null) return;

        Pawn pawn = __instance.pawn;
        if (pawn == null || NechEnergyUtility.IsNecronPawn(pawn)) return;

        HediffDef capacitorDef = NechEnergyUtility.CapacitorSmallDef;
        if (capacitorDef == null) return;

        Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(capacitorDef);
        if (h != null) pawn.health.RemoveHediff(h);
        pawn.needs?.AddOrRemoveNeedsAsAppropriate();
    }
}
