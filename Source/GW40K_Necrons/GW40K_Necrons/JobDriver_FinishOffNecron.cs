using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

public class JobDriver_FinishOffNecron : JobDriver
{
    private Pawn Target => (Pawn)job.targetA.Thing;

    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => Target == null || Target.Dead || !Target.Downed);

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        Toil attack = ToilMaker.MakeToil("FinishOff");
        attack.defaultCompleteMode = ToilCompleteMode.Never;
        attack.tickAction = () =>
        {
            // Interrupt if the pawn was recently damaged — retarget the attacker
            int lastHarm = pawn.mindState?.lastHarmTick ?? -1;
            if (lastHarm > 0 && Find.TickManager.TicksGame - lastHarm < 120)
            {
                Pawn attacker = pawn.mindState.meleeThreat;
                if (attacker == null || attacker.Dead || !attacker.Spawned || !attacker.HostileTo(pawn))
                {
                    attacker = AttackTargetFinder.BestAttackTarget(
                        pawn,
                        TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                        x => x is Pawn { Downed: false },
                        maxDist: 15f) as Pawn;
                }

                if (attacker != null)
                {
                    Job fight = JobMaker.MakeJob(JobDefOf.AttackMelee, attacker);
                    fight.maxNumMeleeAttacks = 3;
                    fight.expiryInterval = 500;
                    pawn.jobs.StartJob(fight, JobCondition.InterruptForced);
                }
                else
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
                }
                return;
            }

            Pawn target = Target;
            if (target == null || target.Dead || !target.Downed)
            {
                ReadyForNextToil();
                return;
            }

            if (pawn.IsHashIntervalTick(60))
            {
                bool hit = pawn.meleeVerbs?.TryMeleeAttack(target, null, surpriseAttack: true) ?? false;
                if (!hit)
                {
                    float dmg = pawn.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS) + 5f;
                    target.TakeDamage(new DamageInfo(DamageDefOf.Blunt, dmg, 0f, -1f, pawn));
                }
            }
        };
        yield return attack;
    }
}
