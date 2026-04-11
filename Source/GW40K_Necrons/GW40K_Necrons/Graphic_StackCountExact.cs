using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Graphic_StackCount replacement that maps stack counts to textures with exact linear
/// interpolation instead of the vanilla formula, which skips textures at certain counts.
/// Use this when you have one texture per stack count and want a true 1:1 mapping.
/// </summary>
public class Graphic_StackCountExact : Graphic_StackCount
{
    public override Graphic SubGraphicFor(Thing thing)
    {
        if (subGraphics.Length <= 1)
            return subGraphics[0];
        if (thing.def.stackLimit <= 1 || subGraphics.Length == 1)
            return subGraphics[0];

        int index = Mathf.RoundToInt(
            (float)(thing.stackCount - 1) / (float)(thing.def.stackLimit - 1)
            * (subGraphics.Length - 1));
        return subGraphics[Mathf.Clamp(index, 0, subGraphics.Length - 1)];
    }
}
