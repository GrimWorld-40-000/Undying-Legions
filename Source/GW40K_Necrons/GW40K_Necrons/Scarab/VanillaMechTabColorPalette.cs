using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Builds the same color list as vanilla <c>MainTabWindow_Mechs.PostOpen</c>
/// (used for “Choose mech accent color” / <see cref="RimWorld.Dialog_ChooseColor"/>).
/// Must only run while playing with <see cref="Find.FactionManager"/> alive — never from
/// <c>[StaticConstructorOnStartup]</c> (that was causing DefDatabase / faction CTDs).
/// </summary>
internal static class VanillaMechTabColorPalette
{
    internal static bool TryBuild(out List<Color> colors)
    {
        colors = null;
        if (Current.ProgramState != ProgramState.Playing || Find.FactionManager == null)
            return false;

        try
        {
            List<Color> list = DefDatabase<ColorDef>.AllDefsListForReading
                .Select(c => c.color)
                .Concat(Find.FactionManager.AllFactionsVisible.Select(f => f.Color))
                .Distinct()
                .ToList();
            list.SortByColor(c => c);
            if (list.Count == 0)
                return false;
            colors = list;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
