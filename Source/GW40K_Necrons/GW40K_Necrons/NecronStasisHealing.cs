using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>Injury load and gradual healing while in <see cref="NecronCasket"/> — closer to Biotech deathrest than a single <c>RemoveAllHediffs</c>.</summary>
public static class NecronStasisHealing
{
    public static float SumHealableInjurySeverity(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null)
            return 0f;
        float sum = 0f;
        foreach (Hediff h in pawn.health.hediffSet.hediffs)
        {
            if (h is not Hediff_Injury inj)
                continue;
            if (inj.IsPermanent())
                continue;
            sum += inj.Severity;
        }
        return sum;
    }

    public static float ExtraStasisHoursFromInjuries(Pawn pawn)
    {
        NecronStasisSettingsDef s = NecronDefOfs.GW40K_NecronStasisSettings;
        float per = s != null && s.injuryHoursPerTotalSeverity > 0f ? s.injuryHoursPerTotalSeverity : 6f;
        float cap = s != null && s.maxInjuryExtraHours > 0f ? s.maxInjuryExtraHours : 48f;
        return Mathf.Min(cap, SumHealableInjurySeverity(pawn) * per);
    }

    /// <summary>Distributes hit-point healing across non-permanent injuries (vanilla-style injury healing).</summary>
    public static void ApplyHealPulse(Pawn pawn, float healPoints)
    {
        if (pawn?.health?.hediffSet == null || healPoints <= 0f)
            return;

        List<Hediff_Injury> injuries = new List<Hediff_Injury>();
        foreach (Hediff h in pawn.health.hediffSet.hediffs)
        {
            if (h is Hediff_Injury inj && !inj.IsPermanent())
                injuries.Add(inj);
        }
        if (injuries.Count == 0)
            return;

        float totalSev = 0f;
        for (int i = 0; i < injuries.Count; i++)
            totalSev += injuries[i].Severity;
        if (totalSev <= 0f)
        {
            float per = healPoints / injuries.Count;
            for (int i = 0; i < injuries.Count; i++)
                injuries[i].Heal(per);
            return;
        }

        for (int i = 0; i < injuries.Count; i++)
        {
            Hediff_Injury inj = injuries[i];
            float share = inj.Severity / totalSev;
            inj.Heal(healPoints * share);
        }
    }

    public static float DeathrestHealEfficiency(Pawn p)
    {
        Gene_Deathrest gene = p?.genes?.GetFirstGeneOfType<Gene_Deathrest>();
        return gene?.DeathrestEfficiency ?? 1f;
    }
}
