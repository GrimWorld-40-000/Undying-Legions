using RimWorld;
using UnityEngine;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Stand-alone visuals for reanimation: additive green glow over the corpse and a ground ring.
/// Mimics the feel of vanilla "rising" effects without patching pawn animation or shambler code.
/// </summary>
[StaticConstructorOnStartup]
public static class ResurrectionProtocolVisuals
{
    private static readonly Material GlowPlaneMat;
    private static readonly Material GlowHaloMat;
    private static readonly Material RingMat;
    private static readonly Material BodyPulseMat;
    private static readonly MaterialPropertyBlock PropBlock = new MaterialPropertyBlock();

    static ResurrectionProtocolVisuals()
    {
        Texture2D radial = CreateRadialFalloffTexture(64);
        GlowPlaneMat = new Material(ShaderDatabase.MoteGlow)
        {
            mainTexture = radial,
            color = new Color(0.22f, 1f, 0.38f, 0.55f)
        };

        GlowHaloMat = new Material(ShaderDatabase.MoteGlow)
        {
            mainTexture = radial,
            color = new Color(0.22f, 1f, 0.38f, 0.22f)
        };

        RingMat = new Material(ShaderDatabase.Transparent)
        {
            mainTexture = radial,
            color = new Color(0.15f, 0.95f, 0.35f, 0.35f)
        };

        // Used for the per-pawn body pulse; alpha driven per-frame via PropBlock.
        BodyPulseMat = new Material(ShaderDatabase.MoteGlow)
        {
            mainTexture = radial,
            color = Color.white
        };
    }

    private static Texture2D CreateRadialFalloffTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size / 2f;
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float dx = (x - half) / half;
            float dy = (y - half) / half;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(1f - dist);
            a *= a;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        tex.Apply();
        tex.name = "GW40K_ResurrectionGlowRadial";
        return tex;
    }

    /// <param name="progress">Hediff severity 0–1; drives pulse speed and visual intensity.</param>
    public static void DrawOverCorpse(Corpse corpse, float progress)
    {
        if (corpse == null || !corpse.Spawned)
            return;

        progress = Mathf.Clamp01(progress);
        // Faster pulsing as the protocol nears completion (mimics "building" energy without touching anim).
        float pulseSpeed = 0.035f + progress * 0.14f;
        float pulse = Mathf.PingPong(Find.TickManager.TicksGame * pulseSpeed, 1f);
        float breathe = 0.65f + 0.35f * pulse;

        Vector3 basePos = corpse.DrawPos;
        float bodyScale = Mathf.Max(corpse.DrawSize.x, corpse.DrawSize.y, 0.8f);

        // Vertical stack: soft glow covering the body silhouette
        Vector3 glowPos = basePos;
        glowPos.y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.02f + Altitudes.AltIncVect.y * 2f;
        float glowScale = bodyScale * (1.05f + 0.15f * breathe + 0.2f * progress);
        Graphics.DrawMesh(
            MeshPool.plane10,
            Matrix4x4.TRS(glowPos, Quaternion.identity, new Vector3(glowScale, 1f, glowScale * 1.15f)),
            GlowPlaneMat,
            0);

        // Second pass: larger / softer (separate material — no shared mutable state)
        glowPos.y += Altitudes.AltIncVect.y * 0.5f;
        float haloScale = glowScale * 1.35f;
        Graphics.DrawMesh(
            MeshPool.plane10,
            Matrix4x4.TRS(glowPos, Quaternion.identity, new Vector3(haloScale, 1f, haloScale * 1.2f)),
            GlowHaloMat,
            0);

        // Ground ring — reads clearly when zoomed out
        Vector3 ringPos = basePos;
        ringPos.y = AltitudeLayer.Building.AltitudeFor() + 0.03f;
        float ringRadius = 0.28f * bodyScale + 0.12f * progress + 0.06f * pulse;
        GenDraw.DrawCircleOutline(ringPos, ringRadius, RingMat);
    }

    /// <summary>
    /// Additive green pulse drawn directly over the pawn body while resurrection is active.
    /// Called from a PawnRenderer.RenderPawnAt postfix so it fires on the pawn render layer.
    /// </summary>
    /// <param name="progress">Hediff severity 0–1. Drives pulse speed, solidity, and final shrink.</param>
    public static void DrawPawnPulse(Vector3 drawLoc, float progress)
    {
        progress = Mathf.Clamp01(progress);

        const float solidThreshold = 0.95f;
        const float baseAlpha      = 0.18f;
        const float pulseRange     = 0.38f;

        float alpha;
        float scaleMult;

        if (progress >= solidThreshold)
        {
            // Last 5%: solid at peak alpha, shrinks to nothing as it completes.
            alpha     = baseAlpha + pulseRange; // fully lit
            float t   = Mathf.InverseLerp(solidThreshold, 1f, progress); // 0 → 1 over last 5%
            scaleMult = 1f - t;                 // 1.0 → 0.0
        }
        else
        {
            // 0–95%: pulse frequency ramps from 1.8 rad/s up to 9.0 rad/s, reaching max at 92%.
            float freq = Mathf.Lerp(1.8f, 9.0f, Mathf.Min(progress / 0.92f, 1f));
            float pulse = Mathf.PingPong(Time.time * freq, 1f);
            alpha     = baseAlpha + pulseRange * pulse;
            scaleMult = 1f;
        }

        if (scaleMult <= 0f) return;

        PropBlock.SetColor("_Color", new Color(0.1f, 1f, 0.28f, alpha));

        Vector3 pos = drawLoc;
        pos.y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.014f;

        Graphics.DrawMesh(
            MeshPool.plane10,
            Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(0.72f * scaleMult, 1f, 1.08f * scaleMult)),
            BodyPulseMat,
            0, null, 0, PropBlock);
    }
}
