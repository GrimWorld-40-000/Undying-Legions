using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Colony nechs and dual-mode weapons (e.g. Cryptek staff of light) often carry multiple <see cref="Verb"/> lanes.
/// Vanilla <see cref="Pawn.TryGetAttackVerb"/> may pick a melee tool for a distant target, which breaks
/// drafted right-click attack ("out of range") and nech command-range checks./// </summary>
internal static class NechIntegratedAttackUtility
{
    /// <summary>
    /// Best non-melee projectile verb by range (Spyder particle beamer, etc.).
    /// </summary>
    /// <param name="requireAvailable">
    /// When true, skips verbs that fail <see cref="Verb.Available"/> (idle AI should not spam jobs during cooldown).
    /// When false, matches ThingDef-linked weapons for UI/command range — avoids hiding the beamer during warmup/recovery.
    /// </param>
    internal static Verb TryGetPreferredRangedVerb(Pawn pawn, bool requireAvailable = false)
    {
        if (pawn?.verbTracker?.AllVerbs == null)
            return null;

        Verb best = null;
        float bestRange = -1f;

        foreach (Verb v in pawn.verbTracker.AllVerbs)
        {
            if (v == null || v.IsMeleeAttack || v.verbProps == null)
                continue;

            // Most integrated / gun verbs are Verb_LaunchProjectile (Verb_Shoot, etc.).
            // Skip non-projectile specials without falling back to melee.
            if (v is not Verb_LaunchProjectile)
                continue;

            // Siege cannon is manual-target only — NEVER include it in auto-attack selection
            // regardless of requireAvailable. The gizmo fires it via TryStartCastOn directly.
            if (v is Verb_SpyderSiegeCannon)
                continue;

            float r = v.verbProps.range;
            if (r < 2f)
                continue;
            if (requireAvailable && !v.Available())
                continue;

            if (r > bestRange)
            {
                bestRange = r;
                best = v;
            }
        }

        return best;
    }
}
