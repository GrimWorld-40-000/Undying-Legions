using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Suppresses high-urgency uncontrolled Nech alerts when the pawn is still under map fog
/// (e.g. inside an unrevealed ancient danger), so the strip does not fire for things you cannot see yet.
/// </summary>
internal static class NechUncontrolledRevealUtility
{
    internal static bool IsRevealedForCriticalUncontrolledAlert(Pawn pawn)
    {
        if (!PawnUtility.ShouldSendNotificationAbout(pawn))
            return false;

        return !HiddenByFog(pawn);
    }

    /// <summary>Spawned pawns whose tile is still fog-covered on their map.</summary>
    private static bool HiddenByFog(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Map == null)
            return false;

        return !pawn.Position.IsValid || pawn.Position.Fogged(pawn.Map);
    }
}
