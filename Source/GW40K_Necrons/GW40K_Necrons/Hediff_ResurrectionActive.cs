using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Applied to the inner pawn of a resurrecting corpse.
/// Severity is advanced externally by CompResurrectible.CompTickRare (pawn health does not tick
/// inside a corpse, so SeverityPerDay and Tick() are both inert here).
/// isFailing is set by CompResurrectible when the pawn's Necrodermis need is below 50%.
/// </summary>
public class Hediff_ResurrectionActive : HediffWithComps
{
    public bool isFailing;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref isFailing, "isFailing");
    }

    public override string LabelInBrackets => isFailing
        ? "failing"
        : $"progress: {Severity * 100f:F0}%";
}
