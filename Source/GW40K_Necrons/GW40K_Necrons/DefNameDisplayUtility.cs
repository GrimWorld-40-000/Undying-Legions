using System.Collections.Generic;
using System.Text.RegularExpressions;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Converts known defName tokens to user-facing labels for inspect/info text.
/// </summary>
public static class DefNameDisplayUtility
{
    private static readonly Regex TokenRegex = new Regex(@"\b[A-Za-z0-9_]+\b", RegexOptions.Compiled);
    private static Dictionary<string, string> _labelsByDefName;

    public static string ReplaceDefNamesWithLabels(string text)
    {
        if (text.NullOrEmpty())
            return text;
        EnsureCache();
        if (_labelsByDefName == null || _labelsByDefName.Count == 0)
            return text;

        return TokenRegex.Replace(text, match =>
        {
            string token = match.Value;
            if (!_labelsByDefName.TryGetValue(token, out string label) || label.NullOrEmpty())
                return token;
            return label;
        });
    }

    private static void EnsureCache()
    {
        if (_labelsByDefName != null)
            return;

        _labelsByDefName = new Dictionary<string, string>();
        AddThingDefs();
        AddNeedDefs();
        AddHediffDefs();
        AddResearchDefs();
        AddPawnKinds();
    }

    private static void AddThingDefs()
    {
        foreach (ThingDef d in DefDatabase<ThingDef>.AllDefsListForReading)
            Add(d?.defName, d?.label);
    }

    private static void AddNeedDefs()
    {
        foreach (NeedDef d in DefDatabase<NeedDef>.AllDefsListForReading)
            Add(d?.defName, d?.label);
    }

    private static void AddHediffDefs()
    {
        foreach (HediffDef d in DefDatabase<HediffDef>.AllDefsListForReading)
            Add(d?.defName, d?.label);
    }

    private static void AddResearchDefs()
    {
        foreach (ResearchProjectDef d in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            Add(d?.defName, d?.label);
    }

    private static void AddPawnKinds()
    {
        foreach (PawnKindDef d in DefDatabase<PawnKindDef>.AllDefsListForReading)
            Add(d?.defName, d?.label);
    }

    private static void Add(string defName, string label)
    {
        if (defName.NullOrEmpty() || label.NullOrEmpty())
            return;
        if (defName == label)
            return;
        if (!_labelsByDefName.ContainsKey(defName))
            _labelsByDefName.Add(defName, label);
    }
}
