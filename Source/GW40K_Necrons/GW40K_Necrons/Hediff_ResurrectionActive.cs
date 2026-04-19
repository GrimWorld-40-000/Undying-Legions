using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Applied to the inner pawn of a resurrecting corpse.
/// Severity is advanced externally by CompResurrectible.CompTickRare (pawn health does not tick
/// inside a corpse, so SeverityPerDay and Tick() are both inert here).
/// </summary>
public class Hediff_ResurrectionActive : HediffWithComps
{
    public override string LabelInBrackets => $"progress: {Severity * 100f:F0}%";
}
