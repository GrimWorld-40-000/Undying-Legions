using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Drafted vanilla mechs use mechanitor/hostile styling; Nechs without a Mechlink can get a hostile-red nameplate.
/// Force a normal colony-friendly name color for player Nechs.
/// </summary>
[HarmonyPatch]
public static class HarmonyPatch_NechNameplateColor
{
    private static MethodBase _target;

    public static bool Prepare()
    {
        foreach (MethodInfo m in typeof(PawnNameColorUtility).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (m.ReturnType != typeof(Color))
                continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length != 1 || ps[0].ParameterType != typeof(Pawn))
                continue;
            if (m.Name.IndexOf("Name", System.StringComparison.OrdinalIgnoreCase) < 0
                && m.Name.IndexOf("Label", System.StringComparison.OrdinalIgnoreCase) < 0
                && m.Name.IndexOf("Plate", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            _target = m;
            return true;
        }

        MethodInfo fallback = typeof(PawnNameColorUtility).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(m => m.ReturnType == typeof(Color)
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == typeof(Pawn));
        _target = fallback;
        return _target != null;
    }

    public static MethodBase TargetMethod() => _target;

    [HarmonyPostfix]
    public static void Postfix(Pawn pawn, ref Color __result)
    {
        if (!NechUtility.IsNechControlled(pawn))
            return;

        MentalStateDef rogueState = NecronDefOfs.GW40K_NechRogue ?? MentalStateDefOf.Berserk;
        if (pawn.InMentalState && pawn.MentalStateDef == rogueState)
        {
            __result = new Color(0.92f, 0.2f, 0.2f);
            return;
        }

        if (pawn.Faction != Faction.OfPlayer)
            return;

        if (!NechInspectStringUtility.IsNechProperlyCommanded(pawn))
        {
            __result = new Color(1f, 0.68f, 0.15f);
            return;
        }

        // Light blue-white — clearly non-hostile, matches typical ally UI.
        __result = new Color(0.72f, 0.88f, 1f);
    }
}
