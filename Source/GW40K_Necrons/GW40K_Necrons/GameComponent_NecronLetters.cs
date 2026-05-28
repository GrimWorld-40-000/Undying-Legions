using HarmonyLib;
using Verse;

#nullable disable
namespace GW40K_Necrons;

[HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
static class HarmonyPatch_RegisterNecronLettersComponent
{
    static void Postfix(Game __instance)
    {
        if (__instance.GetComponent<GameComponent_NecronLetters>() == null)
            __instance.components.Add(new GameComponent_NecronLetters(__instance));
    }
}

/// <summary>Persists one-time intro-letter flags across saves.</summary>
public class GameComponent_NecronLetters : GameComponent
{
    private bool _gaussEnergyIntroShown;

    public GameComponent_NecronLetters(Game game) { }

    public static GameComponent_NecronLetters Current =>
        Verse.Current.Game?.GetComponent<GameComponent_NecronLetters>();

    public bool GaussEnergyIntroShown
    {
        get => _gaussEnergyIntroShown;
        set => _gaussEnergyIntroShown = value;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref _gaussEnergyIntroShown, "gaussEnergyIntroShown", false);
    }
}
