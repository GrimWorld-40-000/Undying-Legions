using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace GW40K_Necrons;

// ── Visual overlay on marked targets ─────────────────────────────────────────

[HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
[HarmonyPatch(new[] { typeof(Vector3), typeof(Rot4?), typeof(bool) })]
public static class HarmonyPatch_HuntersMark_Overlay
{
    private static Material _mat;
    private static Material Mat => _mat ??= MaterialPool.MatFrom(
        "Things/Pawn/Effects/GW40k_Hunted",
        ShaderDatabase.Transparent,
        new Color(1f, 1f, 1f, 0.9f));

    private static Texture2D _glowTex;
    private static Material _glowMat;
    private static Material GlowMat
    {
        get
        {
            if (_glowMat != null) return _glowMat;
            const int size = 64;
            _glowTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size / 2f;
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - dist);
                a *= a; // soft quadratic falloff
                _glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            _glowTex.Apply();
            _glowMat = new Material(ShaderDatabase.MoteGlow)
            {
                mainTexture = _glowTex,
                color = new Color(0.25f, 1f, 0.1f, 0.9f) // bright lime green
            };
            return _glowMat;
        }
    }

    public static void Postfix(PawnRenderer __instance, Vector3 drawLoc)
    {
        try
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn?.health?.hediffSet == null || !pawn.Spawned) return;
            if (!pawn.health.hediffSet.HasHediff(NecronDefOfs.GW40K_HuntersMark)) return;

            Vector3 pos = drawLoc;
            pos.y += Altitudes.AltIncVect.y * 4f;
            pos.z += 0.9f; // above the pawn's head

            // Oval glow behind the icon
            Vector3 glowPos = pos;
            glowPos.y -= Altitudes.AltIncVect.y;
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(glowPos, Quaternion.identity, new Vector3(0.7f, 1f, 0.38f)),
                GlowMat,
                0);

            // Icon on top
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(0.6f, 1f, 0.6f)),
                Mat,
                0);
        }
        catch { }
    }
}

// ── Accuracy bonus for Deathmarks shooting at marked targets ─────────────────

[HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue))]
public static class HarmonyPatch_HuntersMark_Accuracy
{
    public static void Postfix(Thing thing, StatDef stat, ref float __result)
    {
        if (stat != StatDefOf.ShootingAccuracyPawn) return;
        if (thing is not Pawn pawn) return;
        if (pawn.abilities?.GetAbility(NecronDefOfs.GW40K_Deathmark_Phase) == null) return;

        Thing target = pawn.jobs?.curJob?.GetTarget(Verse.AI.TargetIndex.A).Thing;
        if (target is not Pawn targetPawn) return;
        if (!targetPawn.health.hediffSet.HasHediff(NecronDefOfs.GW40K_HuntersMark)) return;

        __result += 0.10f;
    }
}

// ── Bonus damage when a Deathmark hits a marked target ───────────────────────

[HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
public static class HarmonyPatch_HuntersMark_BonusDamage
{
    private const float DamageMultiplier = 1.30f; // +30% damage

    public static void Prefix(Pawn __instance, ref DamageInfo dinfo)
    {
        // Target must carry Hunter's Mark.
        if (!__instance.health.hediffSet.HasHediff(NecronDefOfs.GW40K_HuntersMark)) return;

        // Instigator must be a Deathmark (identified by the Phase ability).
        if (dinfo.Instigator is not Pawn shooter) return;
        if (shooter.abilities?.GetAbility(NecronDefOfs.GW40K_Deathmark_Phase) == null) return;

        dinfo = new DamageInfo(
            dinfo.Def,
            dinfo.Amount * DamageMultiplier,
            armorPenetration: 0.8f,
            dinfo.Angle,
            dinfo.Instigator,
            dinfo.HitPart,
            dinfo.Weapon);

        // Consume the mark — bonuses apply only to the first hit.
        Hediff mark = __instance.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_HuntersMark);
        if (mark != null) __instance.health.RemoveHediff(mark);
    }
}
