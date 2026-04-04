using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using HarmonyLib;

namespace NecronGeneUtil
{
    [StaticConstructorOnStartup]
    public static class StartPatch
    {
        static StartPatch()
        {
            Harmony harmony = new Harmony("FarmerJoe.GWUndyingLegion");
            harmony.PatchAll();
        }
    }
}
