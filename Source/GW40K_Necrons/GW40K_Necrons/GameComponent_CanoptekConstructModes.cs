using System.Collections.Generic;
using HarmonyLib;
using Verse;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
static class HarmonyPatch_RegisterCanoptekConstructModesComponent
{
    static void Postfix(Game __instance)
    {
        if (__instance.GetComponent<GameComponent_CanoptekConstructModes>() == null)
            __instance.components.Add(new GameComponent_CanoptekConstructModes(__instance));
    }
}

/// <summary>
/// Persists per-construct Control Node mode preferences for Canoptek units.
/// Control Node links can override these live, but local values are retained.
/// </summary>
public class GameComponent_CanoptekConstructModes : GameComponent
{
    private Dictionary<int, int>  modeByPawnId         = new Dictionary<int, int>();
    private Dictionary<int, bool> autoModeByPawnId     = new Dictionary<int, bool>();
    private Dictionary<int, int>  defendSetTickByPawnId = new Dictionary<int, int>();

    public GameComponent_CanoptekConstructModes(Game game) { }

    public static GameComponent_CanoptekConstructModes Current =>
        Verse.Current.Game?.GetComponent<GameComponent_CanoptekConstructModes>();

    public ControlNodeMode GetMode(Pawn pawn, ControlNodeMode fallback = ControlNodeMode.Consume)
    {
        if (pawn == null)
            return fallback;
        if (!modeByPawnId.TryGetValue(pawn.thingIDNumber, out int raw))
            return fallback;
        return (ControlNodeMode)raw;
    }

    public void SetMode(Pawn pawn, ControlNodeMode mode)
    {
        if (pawn == null)
            return;
        modeByPawnId[pawn.thingIDNumber] = (int)mode;
        if (mode == ControlNodeMode.Defend)
            defendSetTickByPawnId[pawn.thingIDNumber] = Find.TickManager?.TicksGame ?? 0;
    }

    /// <summary>Returns the game tick when this pawn last entered Defend mode, or 0 if never.</summary>
    public int GetDefendSetTick(Pawn pawn)
    {
        if (pawn == null) return 0;
        return defendSetTickByPawnId.TryGetValue(pawn.thingIDNumber, out int t) ? t : 0;
    }

    public bool GetAutoMode(Pawn pawn)
    {
        if (pawn == null)
            return false;
        return autoModeByPawnId.TryGetValue(pawn.thingIDNumber, out bool v) && v;
    }

    public void SetAutoMode(Pawn pawn, bool auto)
    {
        if (pawn == null)
            return;
        autoModeByPawnId[pawn.thingIDNumber] = auto;
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref modeByPawnId,          "canoptekModeByPawnId",         LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref autoModeByPawnId,      "canoptekAutoModeByPawnId",     LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref defendSetTickByPawnId, "canoptekDefendSetTickByPawnId", LookMode.Value, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            modeByPawnId         ??= new Dictionary<int, int>();
            autoModeByPawnId     ??= new Dictionary<int, bool>();
            defendSetTickByPawnId ??= new Dictionary<int, int>();
        }
    }
}
