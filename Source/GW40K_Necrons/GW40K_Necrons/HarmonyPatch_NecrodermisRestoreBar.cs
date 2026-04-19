using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Overlays a "↑ restoring" label on the GW_UD_Necrodermis and GW40K_NechEnergy need bars
/// while the pawn is inside a stasis crypt, and appends gain-rate info to both tooltips.
/// </summary>
[HarmonyPatch(typeof(Need), nameof(Need.DrawOnGUI))]
public static class HarmonyPatch_StasisRestoreBar
{
    [HarmonyPostfix]
    public static void Postfix(Need __instance, Rect rect, Pawn ___pawn)
    {
        if (!StasisRestoreBarUtil.IsRestoringInStasis(__instance, ___pawn))
            return;

        Color prevColor = GUI.color;
        TextAnchor prevAnchor = Text.Anchor;
        GameFont prevFont = Text.Font;

        GUI.color = new Color(0.55f, 1f, 0.55f, 0.95f);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(rect, "GW40K_StasisRestoring".Translate() + "  ");

        GUI.color = prevColor;
        Text.Anchor = prevAnchor;
        Text.Font = prevFont;
    }
}

[HarmonyPatch(typeof(Need), nameof(Need.GetTipString))]
public static class HarmonyPatch_StasisRestoreTip
{
    [HarmonyPostfix]
    public static void Postfix(Need __instance, ref string __result, Pawn ___pawn)
    {
        if (!StasisRestoreBarUtil.IsRestoringInStasis(__instance, ___pawn))
            return;

        NecronStasisSettingsDef s = NecronDefOfs.GW40K_NecronStasisSettings;
        float gainPerDay = StasisRestoreBarUtil.GainPerDayFor(__instance, s);
        float needed = 1f - __instance.CurLevelPercentage;
        string timeStr = needed > 0.001f
            ? "GW40K_StasisRestoringTipTime".Translate((needed / gainPerDay * 24f).ToString("0.#"))
            : "GW40K_StasisRestoringTipFull".Translate();

        __result += "\n\n" + "GW40K_StasisRestoringTip".Translate(
            gainPerDay.ToStringPercent(),
            timeStr);
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
