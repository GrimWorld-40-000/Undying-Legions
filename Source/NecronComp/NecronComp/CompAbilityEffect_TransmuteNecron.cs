using RimWorld;
using Verse;

#nullable disable
namespace NecronComp;

public class CompAbilityEffect_TransmuteNecron : CompAbilityEffect
{
    public new CompProperties_TransmuteNecron Props => (CompProperties_TransmuteNecron)this.props;
    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest) { }
    public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => false;
    public override bool AICanTargetNow(LocalTargetInfo target) => false;
}
