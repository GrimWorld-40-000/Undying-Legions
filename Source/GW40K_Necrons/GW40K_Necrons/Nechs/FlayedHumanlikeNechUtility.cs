using RimWorld;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Humanlike Flayed colonists use race <c>GW40K_NecronFlayedOne</c> with <see cref="NecronMechExtension"/> — same Nechinator
/// command rules as mechanical Nechs, unlike other humanlike Necron races.
/// </summary>
internal static class FlayedHumanlikeNechUtility
{
    internal static bool IsHumanlikeFlayedNechRace(Pawn p) =>
        p?.RaceProps?.Humanlike == true && p.def?.defName == "GW40K_NecronFlayedOne";
}
