using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Slight horizontal nudge for specific gene icons in xenotype / gene UI (no GeneDef XML field for this).
/// </summary>
[HarmonyPatch(typeof(GeneUIUtility), "DrawGeneBasics")]
public static class HarmonyPatch_GeneUI_IconNudge
{
    private const float WorkflowNudgeLeft = 3f;
    private const float SlowAdvancementNudgeRight = 3f;

    [HarmonyPrefix]
    public static void Prefix(GeneDef gene, ref Rect geneRect, GeneType geneType, bool doBackground, bool clickable, bool overridden)
    {
        if (gene == null)
            return;
        switch (gene.defName)
        {
            case "GW_UD_Workflow":
                geneRect.x -= WorkflowNudgeLeft;
                break;
            case "GW_UD_SlowAdvancement":
                geneRect.x += SlowAdvancementNudgeRight;
                break;
        }
    }
}
