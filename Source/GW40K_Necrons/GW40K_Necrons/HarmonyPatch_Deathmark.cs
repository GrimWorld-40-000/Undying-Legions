using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace GW40K_Necrons;

// ── Gizmo texture fix ─────────────────────────────────────────────────────────

[StaticConstructorOnStartup]
public static class HuntersMarkGizmoTextureFix
{
    static HuntersMarkGizmoTextureFix()
    {
        Texture2D tex = ContentFinder<Texture2D>.Get("UI/Abilities/GW40k_DeathmarkHunt", false);
        if (tex is not null)
            tex.filterMode = FilterMode.Bilinear;
    }
}

// ── Gizmo label: shorten "hunter's mark" → "mark" so it fits on one line ─────

[HarmonyPatch(typeof(Command), "get_Label")]
public static class HarmonyPatch_DeathmarkGizmoLabel
{
    public static void Postfix(Command __instance, ref string __result)
    {
        if (__instance is not Command_Ability abilityCmd) return;
        Ability ability = Traverse.Create(abilityCmd).Field<Ability>("ability").Value;
        if (ability?.def?.defName == "GW40K_Deathmark_HuntersMark")
            __result = "mark";
    }
}

// ── Visual overlay on Hunter's Mark targets ───────────────────────────────────

[StaticConstructorOnStartup]
[HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
[HarmonyPatch(new[] { typeof(Vector3), typeof(Rot4?), typeof(bool) })]
public static class HarmonyPatch_DeathmarkHuntersMark_Overlay
{
    private static readonly Material Mat;
    private static readonly Material GlowMat;

    static HarmonyPatch_DeathmarkHuntersMark_Overlay()
    {
        Mat = MaterialPool.MatFrom(
            "Things/Pawn/Effects/GW40k_Hunted",
            ShaderDatabase.Transparent,
            new Color(1f, 1f, 1f, 0.9f));

        const int size = 64;
        Texture2D glowTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size / 2f;
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float dx = (x - half) / half;
            float dy = (y - half) / half;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(1f - dist);
            a *= a;
            glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        glowTex.Apply();
        GlowMat = new Material(ShaderDatabase.MoteGlow)
        {
            mainTexture = glowTex,
            color = new Color(0.25f, 1f, 0.1f, 0.9f)
        };
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
            pos.z += 0.9f;

            Vector3 glowPos = pos;
            glowPos.y -= Altitudes.AltIncVect.y;
            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(glowPos, Quaternion.identity, new Vector3(0.7f, 1f, 0.38f)),
                GlowMat, 0);

            Graphics.DrawMesh(
                MeshPool.plane10,
                Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(0.6f, 1f, 0.6f)),
                Mat, 0);
        }
        catch { }
    }
}

// ── Shooting accuracy bonus vs marked targets ─────────────────────────────────

[HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue))]
public static class HarmonyPatch_DeathmarkHuntersMark_Accuracy
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

// ── Bonus damage when Deathmark hits a marked target (consumes the mark) ──────

[HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
public static class HarmonyPatch_DeathmarkHuntersMark_BonusDamage
{
    private const float DamageMultiplier = 1.30f;

    public static void Prefix(Pawn __instance, ref DamageInfo dinfo)
    {
        if (!__instance.health.hediffSet.HasHediff(NecronDefOfs.GW40K_HuntersMark)) return;
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

        Hediff mark = __instance.health.hediffSet.GetFirstHediffOfDef(NecronDefOfs.GW40K_HuntersMark);
        if (mark != null) __instance.health.RemoveHediff(mark);
    }
}

// ── Night vision: suppress glow penalty for Deathmark Oculus ─────────────────

[HarmonyPatch(typeof(StatPart_Glow), nameof(StatPart_Glow.TransformValue))]
public static class HarmonyPatch_DeathmarkNightVision
{
    public static bool Prefix(StatPart_Glow __instance, StatRequest req)
    {
        if (__instance.parentStat != StatDefOf.ShootingAccuracyPawn) return true;
        if (!req.HasThing || req.Thing is not Pawn pawn) return true;
        if (pawn.health?.hediffSet?.HasHediff(NecronDefOfs.GW_UD_DeathmarkOculus) == true)
            return false;
        return true;
    }
}
