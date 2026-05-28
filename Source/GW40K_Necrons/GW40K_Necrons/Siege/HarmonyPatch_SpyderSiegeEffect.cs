using HarmonyLib;
using UnityEngine;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Draws a pulsing orange body-silhouette overlay on any Canoptek Spyder that is currently
/// in siege mode (has the GW_UL_SpyderSiegeMode hediff). Mirrors the technique used by
/// HarmonyPatch_NechUncontrolledEffect but uses orange instead of green.
/// </summary>
[StaticConstructorOnStartup]
[HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
[HarmonyPatch(new[] { typeof(Vector3), typeof(Rot4?), typeof(bool) })]
public static class HarmonyPatch_SpyderSiegeEffect
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly MaterialPropertyBlock Mpb = new MaterialPropertyBlock();
    private static readonly Material OverlayMat;

    static HarmonyPatch_SpyderSiegeEffect()
    {
        // Same soft-oval gradient texture as the uncontrolled effect — white so _Color fully
        // controls the tint at render time.
        const int W = 64;
        const int H = 96;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        float cx = W * 0.5f;
        float cy = H * 0.5f;
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
        {
            float dx = (x - cx) / cx;
            float dy = (y - cy) / cy;
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            a = a * a;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        OverlayMat = new Material(ShaderDatabase.MoteGlow) { mainTexture = tex };
    }

    [HarmonyPostfix]
    public static void Postfix(PawnRenderer __instance, Vector3 drawLoc)
    {
        Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
        if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Destroyed) return;
        if (pawn.def?.defName != "UD_Necron_CanoptekSpyder") return;
        if (!HediffComp_SpyderSiegeMode.IsSiegeMode(pawn)) return;

        // Pulse: 0 → peak → 0 over 2 s (same formula as uncontrolled effect).
        float alpha = (1f - Mathf.Cos(Time.time * Mathf.PI)) * 0.30f;
        if (alpha < 0.02f) return;

        Mpb.SetColor(ColorId, new Color(1f, 0.55f, 0.1f, alpha));

        float alt = AltitudeLayer.MoteOverhead.AltitudeFor();
        Vector3 pos = drawLoc;
        pos.y = alt;

        Graphic g = pawn.Graphic;
        float w = g != null ? g.drawSize.x * 2.1f : 3.0f;
        float h = g != null ? g.drawSize.y * 2.1f : 3.0f;
        Graphics.DrawMesh(
            MeshPool.plane10,
            Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(w, 1f, h)),
            OverlayMat, 0, null, 0, Mpb);
    }
}
