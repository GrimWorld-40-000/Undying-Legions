using HarmonyLib;
using NecronGeneUtil;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Need column UI for <see cref="Need_Necrodermis"/> / Nech energy:
/// stasis-crypt restore overlay + tip; dispersion shield repair (change arrow + gold tooltip line).
/// </summary>
public static class HarmonyPatch_NecrodermisRestoreBar
{
    [HarmonyPatch(typeof(Need), nameof(Need.DrawOnGUI))]
    [HarmonyPostfix]
    public static void DrawOnGUI_StasisRestoreMarker(Need __instance, Rect rect, Pawn ___pawn)
    {
        Color prevColor   = GUI.color;
        TextAnchor prevAnchor = Text.Anchor;
        GameFont prevFont = Text.Font;
        Text.Font   = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleRight;

        if (StasisRestoreBarUtil.IsRestoringInStasis(__instance, ___pawn))
        {
            GUI.color = new Color(0.55f, 1f, 0.55f, 0.95f);
            Widgets.Label(rect, "GW40K_StasisRestoring".Translate() + "  ");
        }
        else if (__instance is Need_Necrodermis && ShieldRegenBarUtil.IsRepairingShield(__instance, ___pawn))
        {
            // Show a visible drain indicator on the bar while the dispersion shield repairs.
            GUI.color = ShieldRegenBarUtil.ShieldRepairTooltipGold;
            Widgets.Label(rect, "GW40K_ShieldRepairing".Translate() + "  ");
        }

        GUI.color   = prevColor;
        Text.Anchor = prevAnchor;
        Text.Font   = prevFont;
    }

    [HarmonyPatch(typeof(Need), nameof(Need.GetTipString))]
    [HarmonyPostfix]
    public static void GetTipString_NecrodermisExtras(Need __instance, ref string __result, Pawn ___pawn)
    {
        // Shield repair drain line (Necrodermis need only).
        if (__instance is Need_Necrodermis necroNeed)
        {
            Pawn pawn = ___pawn ?? Traverse.Create(__instance).Field<Pawn>("pawn").Value;
            if (ShieldRegenBarUtil.IsRepairingShield(necroNeed, pawn))
                __result += "\n\n" + ShieldRegenBarUtil.BuildShieldRepairTip(necroNeed);
        }

        // Stasis restore time estimate (Necrodermis and Nech energy needs).
        if (StasisRestoreBarUtil.IsRestoringInStasis(__instance, ___pawn))
        {
            NecronStasisSettingsDef s = NecronDefOfs.GW40K_NecronStasisSettings;
            float gainPerDay = StasisRestoreBarUtil.GainPerDayFor(__instance, s);
            float needed = 1f - __instance.CurLevelPercentage;
            string timeStr = needed > 0.001f
                ? "GW40K_StasisRestoringTipTime".Translate((needed / gainPerDay * 24f).ToString("0.#"))
                : "GW40K_StasisRestoringTipFull".Translate();
            __result += "\n\n" + "GW40K_StasisRestoringTip".Translate(gainPerDay.ToStringPercent(), timeStr);
        }
    }

    [HarmonyPatch(typeof(Need_Necrodermis), nameof(Need.GUIChangeArrow), MethodType.Getter)]
    [HarmonyPostfix]
    public static void GUIChangeArrow_NecrodermisExtras(Need_Necrodermis __instance, ref int __result, Pawn ___pawn)
    {
        // Use Harmony field injection (___pawn) — Traverse.Field("pawn") silently returned
        // null for Need_Necrodermis (NecronGeneUtil type), causing the early return to skip
        // the shield-repair indicator every time.
        Pawn pawn = ___pawn;
        if (pawn == null) return;

        if (__instance.CurLevel < __instance.MaxLevel
            && StasisRestoreBarUtil.IsRestoringInStasis(__instance, pawn))
        {
            __result = 1;
            return;
        }

        if (JobDriver_CanoptekConsume.IsDigestingForNecrodermis(pawn))
        {
            __result = 1;
            return;
        }

        // Shield drain arrow: Need_Necrodermis.GUIChangeArrow already returns -1 when falling,
        // so this doesn't change the arrow value — the text overlay in DrawOnGUI carries the
        // visible signal instead.
        if (__result != 1 && ShieldRegenBarUtil.IsRepairingShield(__instance, pawn))
            __result = -1;
    }
}

internal static class ShieldRegenBarUtil
{
    private const string NecrodermisDefName = "GW_UD_Necrodermis";

    /// <summary>Dispersion-shield repair line in tooltip (gold).</summary>
    internal static readonly Color ShieldRepairTooltipGold = new Color(1f, 0.78f, 0.22f);

    /// <summary>Standard Unity GUI rich-text color tags (Tips / Need often accept these; RimWorld ColoredText does not).</summary>
    internal static string RichTextUnityColor(string plain, Color rgb)
    {
        Color32 c = (Color32)rgb;
        return $"<color=#{c.r:X2}{c.g:X2}{c.b:X2}>{plain}</color>";
    }

    internal static string BuildShieldRepairTip(Need need)
    {
        if (need?.def == null)
            return string.Empty;
        float extraPerDay = need.def.fallPerDay * HarmonyPatch_NecrodermisShieldRegen.ExtraDrainFactor;
        return "GW40K_ShieldRepairingTip".Translate(
            extraPerDay.ToStringPercent(),
            HarmonyPatch_NecrodermisShieldRegen.HpPerDay.ToString("0")).Resolve();
    }

    internal static bool IsRepairingShield(Need need, Pawn pawn)
    {
        if (pawn?.apparel == null) return false;
        if (need.def?.defName != NecrodermisDefName)
            return false;
        foreach (Apparel a in pawn.apparel.WornApparel)
            if (a.def.defName == HarmonyPatch_NecrodermisShieldRegen.ShieldDefName
                && a.HitPoints < a.MaxHitPoints)
                return true;
        return false;
    }
}

internal static class StasisRestoreBarUtil
{
    private const string NecrodermisDefName = "GW_UD_Necrodermis";

    internal static bool IsRestoringInStasis(Need need, Pawn pawn)
    {
        if (pawn == null)
            return false;
        string defName = need.def?.defName;
        if (defName != NecrodermisDefName && need.def != NecronDefOfs.GW40K_NechEnergy)
            return false;
        return ThingOwnerUtility.GetAnyParent<NecronCasket>(pawn) != null;
    }

    internal static float GainPerDayFor(Need need, NecronStasisSettingsDef s)
    {
        if (need.def?.defName == NecrodermisDefName)
            return s != null && s.stasisNecrodermisGainPerDay > 0f ? s.stasisNecrodermisGainPerDay : 1.0f;
        return s != null && s.stasisGaussGainPerDay > 0f ? s.stasisGaussGainPerDay : 1.2f;
    }
}
