using HarmonyLib;
using Verse;

namespace NecronGeneUtil;

[StaticConstructorOnStartup]
public static class StartPatch
{
    static StartPatch()
    {
        new Harmony("FarmerJoe.GWUndyingLegion").PatchAll();
    }
}
