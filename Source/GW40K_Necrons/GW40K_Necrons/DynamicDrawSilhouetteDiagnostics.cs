using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Helpers to find content that may break RimWorld's zoomed-out silhouette pass (<see cref="DynamicDrawManager.DrawSilhouettes"/>).
/// The game does not log which <see cref="Thing"/> failed; this scans <see cref="DynamicDrawManager.DrawThings"/> heuristically.
/// </summary>
public static class DynamicDrawSilhouetteDiagnostics
{
    /// <summary>Set from the debug menu; next <see cref="DynamicDrawManager.DrawSilhouettes"/> prefix runs one scan.</summary>
    public static volatile bool PendingScan;

    internal static void RunScanIfPending(DynamicDrawManager mgr)
    {
        if (!PendingScan || mgr == null)
            return;

        PendingScan = false;

        Log.Message("[GW40K_Necrons] Silhouette diagnostic: scanning DynamicDrawManager.DrawThings (things drawn this frame pass).");

        int total = 0;
        int issues = 0;
        HashSet<string> missingTexLogged = new HashSet<string>();

        foreach (Thing t in mgr.DrawThings)
        {
            total++;

            if (t == null)
            {
                issues++;
                Log.Warning("[GW40K_Necrons] DrawThings contains a null Thing reference.");
                continue;
            }

            if (t.def == null)
            {
                issues++;
                Log.Warning($"[GW40K_Necrons] Thing at {t.Position} (ThingID {t.ThingID}) has null ThingDef.");
                continue;
            }

            try
            {
                Graphic g = t.Graphic;
                if (g == null)
                {
                    issues++;
                    Log.Warning(
                        $"[GW40K_Necrons] Graphic is null: def={t.def.defName} pos={t.Position} thingID={t.ThingID} label={t.LabelCap}");
                    continue;
                }

                // Paths that sometimes correlate with draw/silhouette issues:
                _ = g.ShadowGraphic;
                _ = g.MeshAt(Rot4.North);
            }
            catch (Exception ex)
            {
                issues++;
                Log.Warning(
                    $"[GW40K_Necrons] Graphic/draw probe threw for def={t.def.defName} pos={t.Position} thingID={t.ThingID}: {ex.GetType().Name}: {ex.Message}");
            }

            TryLogGraphicDataPath(t, missingTexLogged, ref issues);
        }

        Log.Message($"[GW40K_Necrons] Silhouette diagnostic finished: {total} things checked, {issues} issue lines.");
    }

    private static void TryLogGraphicDataPath(Thing t, HashSet<string> missingTexLogged, ref int issues)
    {
        ThingDef def = t.def;
        if (def?.graphicData == null)
            return;

        string tex = def.graphicData.texPath;
        if (string.IsNullOrEmpty(tex))
            return;

        string key = def.defName + "|" + tex;
        if (missingTexLogged.Contains(key))
            return;

        if (ContentFinder<Texture2D>.Get(tex, false) == null)
        {
            missingTexLogged.Add(key);
            issues++;
            Log.Warning($"[GW40K_Necrons] Missing texture for def={def.defName} graphicData.texPath=\"{tex}\" (thing {t.ThingID} at {t.Position}).");
        }
    }
}
