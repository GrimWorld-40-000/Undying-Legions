using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
public static class HarmonyPatch_NechInspectString
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance, ref string __result)
    {
        if (__instance?.def?.GetModExtension<NecronMechExtension>() == null)
            return;

        NeedDef mechEnergyDef = DefDatabase<NeedDef>.GetNamedSilentFail("MechEnergy");
        string mechEnergyLabel = mechEnergyDef?.label;
        NeedDef necroNeedDef = DefDatabase<NeedDef>.GetNamedSilentFail("GW_UD_Necrodermis");
        string necroLabel = necroNeedDef?.label ?? "necrodermis";

        List<string> jobLines = new List<string>();
        List<string> otherLines = new List<string>();

        if (!__result.NullOrEmpty())
        {
            string[] lines = __result.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i]?.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (line.IndexOf("Mech energy", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (!mechEnergyLabel.NullOrEmpty()
                    && line.IndexOf(mechEnergyLabel, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (line.IndexOf("Overseer", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                // "Wandering.Status: uncontrolled ..." glued on one line — keep job prefix only.
                if (TryExtractJobBeforeGluedStatus(line, out string jobPrefix))
                {
                    if (!string.IsNullOrEmpty(jobPrefix))
                        jobLines.Add(jobPrefix);
                    continue;
                }

                if (line.IndexOf("Uncontrolled", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (line.IndexOf("necrodermis", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (line.IndexOf(necroLabel, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                if (LooksLikeJobReportLine(line))
                    jobLines.Add(line);
                else
                    otherLines.Add(line);
            }
        }

        StringBuilder sb = new StringBuilder();

        foreach (string j in jobLines)
            sb.AppendLine(j);

        bool commanded = NechInspectStringUtility.IsNechProperlyCommanded(__instance);
        if (commanded)
        {
            Pawn commander = HediffComp_NecronCommandTracker.GetCommanderOf(__instance);
            if (commander != null)
                sb.AppendLine("GW40K_NechCommanderLine".Translate(commander.LabelShortCap).Resolve());
        }
        else
        {
            int sec = __instance.TryGetComp<CompNechUncontrolledTimer>()?.UncontrolledSecondsAtTick(Find.TickManager.TicksGame) ?? 0;
            string uncontrolled = "GW40K_NechUncontrolledLine".Translate(sec).Resolve();
            sb.AppendLine(ColoredText.Colorize(uncontrolled, new Color(0.92f, 0.28f, 0.28f)));
        }

        if (__instance.needs != null)
        {
            Need coreFlux = __instance.needs.TryGetNeed(NecronDefOfs.GW40K_CoreFlux);
            if (coreFlux != null)
                sb.AppendLine("GW40K_NechCoreFluxLine".Translate(coreFlux.CurLevelPercentage.ToStringPercent()).Resolve());

            Need necrodermis = necroNeedDef != null ? __instance.needs.TryGetNeed(necroNeedDef) : null;
            if (necrodermis != null)
                sb.AppendLine("GW40K_NechNecrodermisLine".Translate(necrodermis.CurLevelPercentage.ToStringPercent()).Resolve());
        }

        foreach (string o in otherLines)
            sb.AppendLine(o);

        __result = CollapseInspectNewlines(sb.ToString());
    }

    /// <summary>Lines like "Wandering." / "Moving." — current job report, no colon, ends with a period.</summary>
    private static bool LooksLikeJobReportLine(string line)
    {
        if (line.NullOrEmpty() || line.Contains(":"))
            return false;
        string t = line.TrimEnd();
        if (t.Length < 2 || t[t.Length - 1] != '.')
            return false;
        if (t.Length > 120)
            return false;
        return true;
    }

    /// <summary>Splits "Wandering.Status: ..." into job "Wandering." and drops the glued status tail.</summary>
    private static bool TryExtractJobBeforeGluedStatus(string line, out string jobPrefix)
    {
        jobPrefix = null;
        int idx = line.IndexOf(".status", System.StringComparison.OrdinalIgnoreCase);
        if (idx <= 0)
            return false;
        jobPrefix = line.Substring(0, idx + 1).Trim();
        return true;
    }

    private static string CollapseInspectNewlines(string raw)
    {
        if (raw.NullOrEmpty())
            return string.Empty;
        string[] parts = raw.Split('\n');
        List<string> lines = new List<string>(parts.Length);
        foreach (string part in parts)
        {
            string t = part.Trim();
            if (t.Length > 0)
                lines.Add(t);
        }
        return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
    }
}
