using System.Collections.Generic;
using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Persistent game component that stores Necron Command Group membership.
/// Two groups by default; GroupCount can be raised later without breaking saves
/// (extra groups are silently added on PostLoadInit).
/// </summary>
public class NecronCommandGroupManager : GameComponent
{
    // ── Public constants ────────────────────────────────────────────────────

    public const int GroupCount = 2;

    public static readonly string[] DefaultLabels =
    {
        "Command Group 1",
        "Command Group 2",
    };

    // ── State ───────────────────────────────────────────────────────────────

    // Backing store serialised individually per-group for safe expansion.
    private List<Pawn> _group0 = new List<Pawn>();
    private List<Pawn> _group1 = new List<Pawn>();

    // ── Construction ─────────────────────────────────────────────────────────

    public NecronCommandGroupManager(Game game) { }

    // ── Accessor ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the singleton for the current game, or <c>null</c> when no game is loaded.
    /// Uses <see cref="Verse.Current.Game"/> to avoid naming ambiguity with our own type.
    /// </summary>
    public static NecronCommandGroupManager Instance =>
        Verse.Current.Game?.GetComponent<NecronCommandGroupManager>();

    // ── Query ────────────────────────────────────────────────────────────────

    public List<Pawn> GetGroup(int index) => index switch
    {
        0 => _group0,
        1 => _group1,
        _ => null,
    };

    public string GetLabel(int index) =>
        (uint)index < (uint)DefaultLabels.Length ? DefaultLabels[index] : $"Group {index + 1}";

    /// <summary>Returns the 0-based group index that <paramref name="pawn"/> belongs to, or -1 if none.</summary>
    public int GetGroupOf(Pawn pawn)
    {
        if (pawn == null) return -1;
        if (_group0.Contains(pawn)) return 0;
        if (_group1.Contains(pawn)) return 1;
        return -1;
    }

    // ── Mutation ─────────────────────────────────────────────────────────────

    public void AssignToGroup(Pawn pawn, int groupIndex)
    {
        if (pawn == null) return;
        RemoveFromAllGroups(pawn);
        List<Pawn> target = GetGroup(groupIndex);
        if (target != null && !target.Contains(pawn))
            target.Add(pawn);
    }

    public void RemoveFromAllGroups(Pawn pawn)
    {
        if (pawn == null) return;
        _group0.Remove(pawn);
        _group1.Remove(pawn);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public override void GameComponentTick()
    {
        // Clean up dead / destroyed entries every ~4 s (240 ticks at 60/s).
        if (Find.TickManager.TicksGame % 240 != 0) return;
        _group0.RemoveAll(p => p == null || p.Dead || p.Destroyed);
        _group1.RemoveAll(p => p == null || p.Dead || p.Destroyed);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref _group0, "cmdGroup0", LookMode.Reference);
        Scribe_Collections.Look(ref _group1, "cmdGroup1", LookMode.Reference);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _group0 ??= new List<Pawn>();
            _group1 ??= new List<Pawn>();
            _group0.RemoveAll(p => p == null || p.Dead || p.Destroyed);
            _group1.RemoveAll(p => p == null || p.Dead || p.Destroyed);
        }
    }
}
