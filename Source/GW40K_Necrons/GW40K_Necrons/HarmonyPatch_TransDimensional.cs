using HarmonyLib;
using UnityEngine;
using Verse;

namespace GW40K_Necrons;

// While a pawn has GW40K_TransDimensional they are "out of phase":
//   1. All incoming damage is absorbed.
//   2. They cannot fire projectiles.
//   3. They render with a semi-transparent green tint + darker hex pattern on top.

[HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
public static class HarmonyPatch_TransDimensional_AbsorbDamage
{
    public static void Prefix(Pawn __instance, ref bool absorbed)
    {
        if (absorbed) return;
        if (__instance.health.hediffSet.HasHediff(NecronDefOfs.GW40K_TransDimensional))
            absorbed = true;
    }
}

[HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
public static class HarmonyPatch_TransDimensional_BlockShooting
{
    public static bool Prefix(Verb_LaunchProjectile __instance)
    {
        Pawn caster = __instance.CasterPawn;
        if (caster != null && caster.health.hediffSet.HasHediff(NecronDefOfs.GW40K_TransDimensional))
            return false;
        return true;
    }
}

[HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
[HarmonyPatch(new[] { typeof(Vector3), typeof(Rot4?), typeof(bool) })]
public static class HarmonyPatch_TransDimensional_GreenOverlay
{
    // Layer 1: plain green gradient — tints the entire unit green.
    private static Material _baseMat;
    private static Material BaseMat => _baseMat ??= BuildBaseMat();

    // Layer 2: hexagonal border pattern drawn on top at ~50% opacity.
    private static Material _hexMat;
    private static Material HexMat => _hexMat ??= BuildHexMat();

    // ── Layer 1: soft radial green gradient ────────────────────────────────
    private static Material BuildBaseMat()
    {
        const int size = 64;
        float     half = size * 0.5f;
        var       tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float dx    = x - half;
            float dy    = y - half;
            float dist  = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist / half), 0.6f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();

        // Base green colour — 50% opacity at peak.
        return new Material(ShaderDatabase.Transparent)
        {
            mainTexture = tex,
            color       = new Color(0.05f, 0.80f, 0.25f, 0.5f)
        };
    }

    // ── Layer 2: hex border pattern — fills transparent, borders dark green ─
    private static Material BuildHexMat()
    {
        const int size    = 128;
        float     half    = size * 0.5f;
        float     hexR    = size / 9f;        // ~14 px circumradius — ~6 hexes across
        float     borderW = hexR * 0.22f;     // Voronoi border thickness
        float     sqrt3   = Mathf.Sqrt(3f);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int xi = 0; xi < size; xi++)
        for (int yi = 0; yi < size; yi++)
        {
            float px = xi - half;
            float py = yi - half;

            // Radial alpha — hex pattern fades out toward the edge.
            float radDist = Mathf.Sqrt(px * px + py * py);
            float alpha   = Mathf.Pow(Mathf.Clamp01(1f - radDist / half), 0.6f);
            if (alpha < 0.01f) { tex.SetPixel(xi, yi, Color.clear); continue; }

            // Pointy-top axial hex coordinates.
            float qF = (sqrt3 / 3f * px - py / 3f) / hexR;
            float rF = (2f / 3f * py)              / hexR;

            // Cube-coordinate rounding → nearest hex.
            float sF = -qF - rF;
            int   qI = Mathf.RoundToInt(qF);
            int   rI = Mathf.RoundToInt(rF);
            int   sI = Mathf.RoundToInt(sF);
            float dq = Mathf.Abs(qI - qF);
            float dr = Mathf.Abs(rI - rF);
            float ds = Mathf.Abs(sI - sF);
            if      (dq > dr && dq > ds) qI = -rI - sI;
            else if (dr > ds)            rI = -qI - sI;

            // Voronoi: two nearest hex centres.
            float d1 = float.MaxValue, d2 = float.MaxValue;
            for (int dqn = -1; dqn <= 1; dqn++)
            for (int drn = -1; drn <= 1; drn++)
            {
                float cx = hexR * (sqrt3 * (qI + dqn) + sqrt3 * 0.5f * (rI + drn));
                float cy = hexR * 1.5f * (rI + drn);
                float d  = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                if      (d < d1) { d2 = d1; d1 = d; }
                else if (d < d2) {           d2 = d; }
            }

            bool onBorder = (d2 - d1) < borderW;

            // Border pixels: dark green at full local alpha.
            // Interior pixels: fully transparent (the base green layer shows through).
            Color c = onBorder
                ? new Color(0.4f, 1f, 0.2f, alpha)
                : Color.clear;
            tex.SetPixel(xi, yi, c);
        }

        tex.Apply();
        // Draw at ~50% opacity via material alpha.
        return new Material(ShaderDatabase.Transparent)
        {
            mainTexture = tex,
            color       = new Color(1f, 1f, 1f, 0.5f)
        };
    }

    // ── Postfix: draw both layers ──────────────────────────────────────────
    public static void Postfix(PawnRenderer __instance, Vector3 drawLoc)
    {
        Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
        if (pawn?.health?.hediffSet == null || !pawn.Spawned) return;
        if (!pawn.health.hediffSet.HasHediff(NecronDefOfs.GW40K_TransDimensional)) return;

        Vector3 pos  = drawLoc;
        pos.y += Altitudes.AltIncVect.y * 3f;
        var scale    = new Vector3(1.2f, 1f, 1.5f);
        var matrix   = Matrix4x4.TRS(pos, Quaternion.identity, scale);

        // Layer 1 — base green tint.
        Graphics.DrawMesh(MeshPool.plane10, matrix, BaseMat, 0);

        // Layer 2 — hex borders on top, slightly higher to avoid z-fighting.
        Vector3 hexPos = pos;
        hexPos.y += Altitudes.AltIncVect.y;
        Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(hexPos, Quaternion.identity, scale), HexMat, 0);
    }
}
