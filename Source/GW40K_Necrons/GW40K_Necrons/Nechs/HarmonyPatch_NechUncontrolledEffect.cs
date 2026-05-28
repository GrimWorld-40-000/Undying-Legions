using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Draw a full-body green pulse on any Nech that lacks a valid Nechinator command link.
/// Uses a programmatically generated soft-oval gradient texture (same approach as the
/// Deathmark Hunter's Mark overlay) so the color is fully controlled — MoteGlow additive
/// blending with the pawn's own texture was causing the pawn's orange/red hues to override
/// the intended green.
/// </summary>
[StaticConstructorOnStartup]
[HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
[HarmonyPatch(new[] { typeof(Vector3), typeof(Rot4?), typeof(bool) })]
public static class HarmonyPatch_NechUncontrolledEffect
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly MaterialPropertyBlock Mpb = new MaterialPropertyBlock();
    private static readonly Material OverlayMat;

    static HarmonyPatch_NechUncontrolledEffect()
    {
        // Tall soft-oval gradient: white pixels fading to transparent at the edges.
        // Width:height 2:3 matches the rough proportions of a standing humanlike pawn.
        // MoteGlow multiplies _Color.rgb by this texture; since the texture is white,
        // the final colour is purely _Color.rgb — we stay in full control.
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
            a = a * a;  // quadratic fall-off for softer edges
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
        if (!NechUtility.IsNechControlled(pawn)) return;
        if (pawn.Faction != Faction.OfPlayer) return;
        if (NechInspectStringUtility.IsNechProperlyCommanded(pawn)) return;

        // Cos gives a smooth 0 → peak → 0 over 2 s.
        // Abs(Sin) was doubling the frequency by folding negatives back up.
        // Rogue pawns pulse faster (1.8× frequency) and flash red instead of green.
        bool isRogue = pawn.MentalStateDef != null
            && pawn.MentalStateDef == NecronDefOfs.GW40K_NechRogue;

        float freq  = isRogue ? Mathf.PI * 1.8f : Mathf.PI;
        float peak  = isRogue ? 0.30f : 0.225f;
        float alpha = (1f - Mathf.Cos(Time.time * freq)) * peak;
        if (alpha < 0.02f) return;

        Color overlayColor = isRogue
            ? new Color(0.90f, 0.20f, 0.15f, alpha)   // red — rogue
            : new Color(0.35f, 0.72f, 0.52f, alpha);   // green — uncontrolled

        Mpb.SetColor(ColorId, overlayColor);

        float alt = AltitudeLayer.MoteOverhead.AltitudeFor();
        Vector3 pos = drawLoc;
        pos.y = alt;

        if (pawn.RaceProps.Humanlike)
        {
            // Shift centre slightly down: head ~+0.32 above drawLoc, feet ~-0.7 below.
            // Midpoint = -0.19. Scale covers feet → top of head with a small margin.
            pos.z -= 0.19f;
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(1.7f, 1f, 2.3f)),
                OverlayMat, 0, null, 0, Mpb);
        }
        else
        {
            Graphic g = pawn.Graphic;
            float w = g != null ? g.drawSize.x * 2.1f : 1.8f;
            float h = g != null ? g.drawSize.y * 2.1f : 1.8f;
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(w, 1f, h)),
                OverlayMat, 0, null, 0, Mpb);
        }
    }
}
