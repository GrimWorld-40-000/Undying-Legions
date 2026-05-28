using RimWorld;
using Verse;
using Verse.AI.Group;

namespace GW40K_Necrons;

/// <summary>
/// Dispatch node for the Necron siege withdrawal. Rolls one of three outcomes
/// on entry and announces it; the lord graph then routes to the matching branch.
///
///   0 – Content:  satisfied with damage, pursuing other targets → immediate exit.
///   1 – Steal:    loot what they can            → LordToil_NecronSteal → exit.
///   2 – Kidnap:   take captives for conversion  → LordToil_NecronKidnap → exit.
///
/// This toil has no duties of its own — it exists for exactly one tick before
/// the Trigger_TickCondition routes to the appropriate branch.
/// </summary>
public class LordToil_NecronWithdraw : LordToil
{
    private class Data : LordToilData
    {
        public int outcome = -1;
        public override void ExposeData() =>
            Scribe_Values.Look(ref outcome, "withdrawOutcome", -1);
    }

    private Data D => (Data)(data ??= new Data());

    public LordToil_NecronWithdraw() { data = new Data(); }

    /// <summary>Outcome index set in <see cref="Init"/>; -1 until then.</summary>
    public int ChosenOutcome => D.outcome;

    public override void Init()
    {
        base.Init();
        if (D.outcome >= 0) return; // restored from save

        D.outcome = Rand.RangeInclusive(0, 2);

        string msgKey = D.outcome switch
        {
            1 => "GW40K_NecronWithdrawSteal",
            2 => "GW40K_NecronWithdrawKidnap",
            _ => "GW40K_NecronWithdrawContent"
        };

        // historical: true — logged so players can review the siege outcome.
        Messages.Message(
            msgKey.Translate(lord.faction.Name.Named("FACTION")),
            MessageTypeDefOf.NeutralEvent, historical: true);
    }

    public override void UpdateAllDuties()
    {
        // One-tick dispatch node — no duties to assign.
    }
}
