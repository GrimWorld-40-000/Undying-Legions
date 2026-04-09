using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Monolith bills: vanilla <see cref="WorkGiver_DoBill"/> flow, plus per-recipe Command Protocol bandwidth
/// for <see cref="RecipeExtension_SpawnMech"/> (other Monolith recipes unchanged).
/// </summary>
public class WorkGiver_MonolithSummon : WorkGiver_DoBill
{
    /// <summary>
    /// RimWorld 1.6+ may use instance <c>TryFindBestBillIngredients</c> or a different static overload than older
    /// Harmony <see cref="AccessTools.MethodDelegate{TDelegate}"/> was compiled for — delegate binding throws and
    /// bricks the type initializer. Invoke via <see cref="MethodInfo"/> instead.
    /// </summary>
    private static readonly MethodInfo MiTryFindBestBillIngredients = FindTryFindBestBillIngredientsMethod();

    private static readonly MethodInfo MiClosestUnfinishedThingForBill =
        AccessTools.DeclaredMethod(typeof(WorkGiver_DoBill), "ClosestUnfinishedThingForBill");

    private static readonly MethodInfo MiFinishUftJob =
        AccessTools.DeclaredMethod(typeof(WorkGiver_DoBill), "FinishUftJob");

    private static MethodInfo FindTryFindBestBillIngredientsMethod()
    {
        MethodInfo[] methods = typeof(WorkGiver_DoBill).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        MethodInfo fallback = null;
        foreach (MethodInfo m in methods)
        {
            if (m.Name != "TryFindBestBillIngredients")
                continue;
            fallback = m;
            ParameterInfo[] p = m.GetParameters();
            // Legacy / common: (Bill, Pawn, Thing|IBillGiver, List<ThingCount>)
            if (p.Length == 4
                && p[0].ParameterType == typeof(Bill)
                && p[1].ParameterType == typeof(Pawn)
                && (p[2].ParameterType == typeof(Thing) || p[2].ParameterType == typeof(IBillGiver))
                && p[3].ParameterType == typeof(List<ThingCount>))
                return m;
        }

        return fallback;
    }

    private bool InvokeTryFindBestBillIngredients(Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen)
    {
        if (MiTryFindBestBillIngredients == null)
            return false;
        object[] args = BuildTryFindBestBillIngredientsArgs(MiTryFindBestBillIngredients, bill, pawn, billGiver, chosen);
        if (args == null)
            return false;
        object target = MiTryFindBestBillIngredients.IsStatic ? null : this;
        return MiTryFindBestBillIngredients.Invoke(target, args) is bool b && b;
    }

    /// <summary>Verse defines its own generic <c>Predicate&lt;,&gt;</c>; bill helpers use <see cref="System.Predicate{T}"/>.</summary>
    private static bool IsSystemPredicate1(Type t) =>
        t.IsGenericType && t.GetGenericTypeDefinition().Name == "Predicate`1" && t.Namespace == "System";

    private static object[] BuildTryFindBestBillIngredientsArgs(
        MethodInfo mi,
        Bill bill,
        Pawn pawn,
        Thing billGiver,
        List<ThingCount> chosen)
    {
        ParameterInfo[] ps = mi.GetParameters();
        object[] a = new object[ps.Length];

        bool hasBill = false;
        bool hasPawn = false;
        bool hasList = false;
        bool hasGiver = false;
        for (int i = 0; i < ps.Length; i++)
        {
            Type t = ps[i].ParameterType;
            if (t == typeof(Bill))
                hasBill = true;
            else if (t == typeof(Pawn))
                hasPawn = true;
            else if (t == typeof(List<ThingCount>))
                hasList = true;
            else if (t == typeof(Thing) || t == typeof(IBillGiver))
                hasGiver = true;
        }

        if (!hasBill || !hasPawn || !hasList || !hasGiver)
        {
            Log.ErrorOnce(
                $"GW40K_Necrons: TryFindBestBillIngredients signature not handled ({ps.Length} params) — Monolith bills need an update.",
                0x4E3C72A2);
            return null;
        }

        for (int i = 0; i < ps.Length; i++)
        {
            Type t = ps[i].ParameterType;
            if (t == typeof(Bill))
                a[i] = bill;
            else if (t == typeof(Pawn))
                a[i] = pawn;
            else if (t == typeof(Thing))
                a[i] = billGiver;
            else if (t == typeof(IBillGiver))
                a[i] = billGiver;
            else if (t == typeof(List<ThingCount>))
                a[i] = chosen;
            else if (t == typeof(bool))
                a[i] = false;
            else if (IsSystemPredicate1(t))
                a[i] = null;
            else
            {
                Log.ErrorOnce(
                    $"GW40K_Necrons: TryFindBestBillIngredients unmapped parameter {t.FullName}",
                    0x4E3C72A3);
                return null;
            }
        }

        return a;
    }

    private UnfinishedThing InvokeClosestUnfinishedThingForBill(Pawn pawn, Bill_ProductionWithUft bill)
    {
        if (MiClosestUnfinishedThingForBill == null)
            return null;
        object[] args = BuildClosestUftArgs(MiClosestUnfinishedThingForBill, pawn, bill);
        object target = MiClosestUnfinishedThingForBill.IsStatic ? null : this;
        return MiClosestUnfinishedThingForBill.Invoke(target, args) as UnfinishedThing;
    }

    private static object[] BuildClosestUftArgs(MethodInfo mi, Pawn pawn, Bill_ProductionWithUft bill)
    {
        ParameterInfo[] ps = mi.GetParameters();
        object[] a = new object[ps.Length];
        for (int i = 0; i < ps.Length; i++)
        {
            Type t = ps[i].ParameterType;
            if (t == typeof(Pawn))
                a[i] = pawn;
            else if (t == typeof(Bill_ProductionWithUft))
                a[i] = bill;
            else if (t == typeof(Bill_Production))
                a[i] = bill;
            else if (t == typeof(bool))
                a[i] = false;
            else if (IsSystemPredicate1(t))
                a[i] = null;
            else
                Log.ErrorOnce($"GW40K_Necrons: ClosestUnfinishedThingForBill unmapped param {t.FullName}", 0x4E3C72A4);
        }

        return a;
    }

    private Job InvokeFinishUftJob(Pawn pawn, UnfinishedThing uft, Bill_ProductionWithUft bill)
    {
        if (MiFinishUftJob == null)
            return null;
        object[] args = BuildFinishUftArgs(MiFinishUftJob, pawn, uft, bill);
        object target = MiFinishUftJob.IsStatic ? null : this;
        return MiFinishUftJob.Invoke(target, args) as Job;
    }

    private static object[] BuildFinishUftArgs(MethodInfo mi, Pawn pawn, UnfinishedThing uft, Bill_ProductionWithUft bill)
    {
        ParameterInfo[] ps = mi.GetParameters();
        object[] a = new object[ps.Length];
        for (int i = 0; i < ps.Length; i++)
        {
            Type t = ps[i].ParameterType;
            if (t == typeof(Pawn))
                a[i] = pawn;
            else if (t == typeof(UnfinishedThing))
                a[i] = uft;
            else if (t == typeof(Bill_ProductionWithUft))
                a[i] = bill;
            else if (t == typeof(Bill_Production))
                a[i] = bill;
            else if (t == typeof(bool))
                a[i] = false;
            else
                Log.ErrorOnce($"GW40K_Necrons: FinishUftJob unmapped param {t.FullName}", 0x4E3C72A5);
        }

        return a;
    }

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        if (base.ShouldSkip(pawn, forced))
            return true;
        if (pawn.health?.hediffSet?.GetFirstHediffOfDef(
                HediffDef.Named("GW40K_CommandProtocolImplant")) == null)
            return true;
        return false;
    }

    public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
    {
        IBillGiver billGiver = thing as IBillGiver;
        if (billGiver != null && ThingIsUsableBillGiver(thing) && billGiver.BillStack.AnyShouldDoNow
            && billGiver.UsableForBillsAfterFueling())
        {
            LocalTargetInfo target = thing;
            if (pawn.CanReserve(target, 1, -1, null, forced) && !thing.IsBurning() && !thing.IsForbidden(pawn))
            {
                CompRefuelable compRefuelable = thing.TryGetComp<CompRefuelable>();
                if (compRefuelable == null || compRefuelable.HasFuel)
                {
                    billGiver.BillStack.RemoveIncompletableBills();
                    return MonolithStartOrResumeBillJob(pawn, billGiver);
                }

                if (!RefuelWorkGiverUtility.CanRefuel(pawn, thing, forced))
                    return null;
                return RefuelWorkGiverUtility.RefuelJob(pawn, thing, forced, null, null);
            }
        }

        return null;
    }

    /// <summary>
    /// Mirrors <c>WorkGiver_DoBill.StartOrResumeBillJob</c> with a summon-bandwidth gate before ingredient search.
    /// </summary>
    private Job MonolithStartOrResumeBillJob(Pawn pawn, IBillGiver giver)
    {
        if (MiTryFindBestBillIngredients == null)
        {
            Log.ErrorOnce(
                "GW40K_Necrons: WorkGiver_DoBill.TryFindBestBillIngredients not found — Monolith work giver cannot run.",
                0x4E3C72A1);
            return null;
        }

        List<ThingCount> chosenIngThings = new List<ThingCount>();
        for (int i = 0; i < giver.BillStack.Count; i++)
        {
            Bill bill = giver.BillStack[i];
            if (bill.recipe.requiredGiverWorkType != null && bill.recipe.requiredGiverWorkType != def.workType)
                continue;

            if (!bill.ShouldDoNow())
                continue;

            if (!bill.PawnAllowedToStartAnew(pawn))
                continue;

            SkillRequirement skillRequirement = bill.recipe.FirstSkillRequirementPawnDoesntSatisfy(pawn);
            if (skillRequirement != null)
            {
                JobFailReason.Is("UnderRequiredSkill".Translate(skillRequirement.minLevel), bill.Label);
                continue;
            }

            Bill_ProductionWithUft billProductionWithUft = bill as Bill_ProductionWithUft;
            if (billProductionWithUft != null)
            {
                if (billProductionWithUft.BoundUft != null)
                {
                    if (billProductionWithUft.BoundWorker != pawn
                        || !pawn.CanReserveAndReach(billProductionWithUft.BoundUft, PathEndMode.Touch, Danger.Deadly, 1, -1, null, false)
                        || billProductionWithUft.BoundUft.IsForbidden(pawn))
                    {
                        continue;
                    }

                    Job finish = InvokeFinishUftJob(pawn, billProductionWithUft.BoundUft, billProductionWithUft);
                    if (finish != null)
                        return finish;
                }
                else
                {
                    UnfinishedThing unfinishedThing = InvokeClosestUnfinishedThingForBill(pawn, billProductionWithUft);
                    if (unfinishedThing != null)
                    {
                        Job finish = InvokeFinishUftJob(pawn, unfinishedThing, billProductionWithUft);
                        if (finish != null)
                            return finish;
                    }
                }
            }

            RecipeExtension_SpawnMech spawnExt = bill.recipe?.GetModExtension<RecipeExtension_SpawnMech>();
            if (spawnExt?.mechKindDef != null)
            {
                HediffComp_NecronCommandTracker tracker = HediffComp_NecronCommandTracker.GetTracker(pawn);
                float cost = tracker != null
                    ? tracker.CommandBandwidthCostForPawnKind(spawnExt.mechKindDef)
                    : float.PositiveInfinity;
                if (tracker == null || !tracker.HasBandwidthFor(cost))
                {
                    if (FloatMenuMakerMap.makingFor == pawn)
                        JobFailReason.Is("GW40K_CommandBandwidthFullSummon".Translate(bill.Label), bill.Label);
                    continue;
                }
            }

            if (InvokeTryFindBestBillIngredients(bill, pawn, (Thing)giver, chosenIngThings))
            {
                Job result = TryStartNewDoBillJob(pawn, bill, giver, chosenIngThings);
                chosenIngThings.Clear();
                return result;
            }

            if (FloatMenuMakerMap.makingFor == pawn)
                JobFailReason.Is("MissingMaterials".Translate(), bill.Label);

            chosenIngThings.Clear();
        }

        chosenIngThings.Clear();
        return null;
    }

    /// <summary>Same as vanilla <c>WorkGiver_DoBill.TryStartNewDoBillJob</c>, using a supplied ingredient list.</summary>
    private static Job TryStartNewDoBillJob(Pawn pawn, Bill bill, IBillGiver giver, List<ThingCount> chosenIngThings)
    {
        Job haulOff = WorkGiverUtility.HaulStuffOffBillGiverJob(pawn, giver, null);
        if (haulOff != null)
            return haulOff;

        Job job = new Job(JobDefOf.DoBill, (Thing)giver);
        job.targetQueueB = new List<LocalTargetInfo>(chosenIngThings.Count);
        job.countQueue = new List<int>(chosenIngThings.Count);
        for (int i = 0; i < chosenIngThings.Count; i++)
        {
            job.targetQueueB.Add(chosenIngThings[i].Thing);
            job.countQueue.Add(chosenIngThings[i].Count);
        }

        job.haulMode = HaulMode.ToCellNonStorage;
        job.bill = bill;
        return job;
    }
}
