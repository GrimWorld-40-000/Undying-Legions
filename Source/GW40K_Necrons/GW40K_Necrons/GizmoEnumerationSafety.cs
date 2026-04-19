using System;
using System.Collections.Generic;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Other mods (and occasionally vanilla) can throw while enumerating <see cref="Pawn.GetGizmos"/>.
/// Wrapping upstream enumeration avoids red error spam and preserves any gizmos already yielded.
/// Known trigger: many units selected at once (mixed melee/ranged); squad attack gizmo calls into
/// <c>FloatMenuUtility.UseRangedAttack</c> and one odd pawn in the selection can NRE. Scarab-style pawns may need a dedicated path later.
/// </summary>
internal static class GizmoEnumerationSafety
{
    internal static IEnumerable<Gizmo> PassThroughWithSafety(IEnumerable<Gizmo> upstream, Pawn pawn, string tag)
    {
        if (upstream == null)
            yield break;

        IEnumerator<Gizmo> e;
        try
        {
            e = upstream.GetEnumerator();
        }
        catch (Exception ex)
        {
            LogUpstreamFailure(ex, pawn, tag);
            yield break;
        }

        using (e)
        {
            while (true)
            {
                Gizmo g;
                try
                {
                    if (!e.MoveNext())
                        break;
                    g = e.Current;
                }
                catch (Exception ex)
                {
                    LogUpstreamFailure(ex, pawn, tag);
                    yield break;
                }

                yield return g;
            }
        }
    }

    private static void LogUpstreamFailure(Exception ex, Pawn pawn, string tag)
    {
        int key = unchecked(tag.GetHashCode() * 397 ^ (pawn?.thingIDNumber ?? 0));
        Log.WarningOnce(
            $"[GW40K_Necrons] GetGizmos upstream failed ({tag}) for {pawn?.LabelShort ?? "?"}: {ex.GetType().Name}: {ex.Message}",
            key);
    }
}
