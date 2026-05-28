using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Sound;

namespace GW40K_Necrons;

/// <summary>
/// Fires one-shot sounds after a configurable tick delay.
/// Used by <see cref="Projectile_DelayedImpactSound"/> to offset the particle
/// beamer boom ~1 second after visual impact.
///
/// Entries are intentionally NOT persisted across saves: if the game is saved
/// while a shell is in-flight the delayed boom simply won't play on load.
/// That edge-case is acceptable — reloading a save already resets audio state.
/// </summary>
public class GameComponent_DelayedSound : GameComponent
{
    // Quick static accessor so callers don't need to look up the component each call.
    public static GameComponent_DelayedSound Instance =>
        Verse.Current.Game?.GetComponent<GameComponent_DelayedSound>();

    private readonly List<Entry> pending = new();

    public GameComponent_DelayedSound(Game game) { }

    /// <summary>Queues <paramref name="sound"/> to play at <paramref name="pos"/> after <paramref name="delayTicks"/> ticks.</summary>
    public void Schedule(SoundDef sound, IntVec3 pos, Map map, int delayTicks)
    {
        if (sound == null || map == null) return;
        pending.Add(new Entry(Find.TickManager.TicksGame + delayTicks, sound, pos, map));
    }

    public override void GameComponentTick()
    {
        if (pending.Count == 0) return;

        int now = Find.TickManager.TicksGame;
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            Entry e = pending[i];
            if (now < e.FireTick) continue;

            // Only play if the map is still loaded.
            if (e.Map != null && !e.Map.Disposed)
                e.Sound.PlayOneShot(new TargetInfo(e.Pos, e.Map));

            pending.RemoveAt(i);
        }
    }

    // ── Inner type ────────────────────────────────────────────────────────────

    private readonly struct Entry
    {
        public readonly int      FireTick;
        public readonly SoundDef Sound;
        public readonly IntVec3  Pos;
        public readonly Map      Map;

        public Entry(int fireTick, SoundDef sound, IntVec3 pos, Map map)
        {
            FireTick = fireTick;
            Sound    = sound;
            Pos      = pos;
            Map      = map;
        }
    }
}

/// <summary>Auto-registers <see cref="GameComponent_DelayedSound"/> on every game load.</summary>
[HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
static class HarmonyPatch_RegisterDelayedSound
{
    static void Postfix(Game __instance)
    {
        if (__instance.GetComponent<GameComponent_DelayedSound>() == null)
            __instance.components.Add(new GameComponent_DelayedSound(__instance));
    }
}
