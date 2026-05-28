using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>Injury load and gradual healing while in <see cref="NecronCasket"/> — closer to Biotech deathrest than a single <c>RemoveAllHediffs</c>.</summary>
public static class NecronStasisHealing
{
    /// <summary>
    /// Backing field on <see cref="Hediff"/> so we can reduce severity without triggering
    /// <see cref="Pawn_HealthTracker.Notify_HediffChanged"/> (and the per-wound state-change
    /// notifications that can fire "fully healed" prematurely).  The health tick sees
    /// <see cref="Hediff.ShouldRemove"/> on the next pass and removes zero-severity injuries
    /// naturally — causing <c>MessageFullyHealed</c> to fire exactly once.
    /// </summary>
    /// <summary>RimWorld 1.6+ uses <c>severityInt</c> on <see cref="Hediff"/>; older builds used <c>severity</c>.</summary>
    private static readonly FieldInfo s_severityField =
        AccessTools.Field(typeof(Hediff), "severityInt")
        ?? AccessTools.Field(typeof(Hediff), "severity");

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

    /// <summary>
    /// Distributes hit-point healing across non-permanent injuries.
    /// Writes the severity backing field directly — bypassing <see cref="Pawn_HealthTracker.Notify_HediffChanged"/>
    /// and any setter-level auto-removal — so no per-wound state-change fires during the loop.
    /// The vanilla health tick sees <see cref="Hediff.ShouldRemove"/> on its next pass and
    /// removes zero-severity injuries in one batch, causing <c>MessageFullyHealed</c> to fire once.
    /// </summary>
    public static void ApplyHealPulse(Pawn pawn, float healPoints)
    {
        if (pawn?.health?.hediffSet == null || healPoints <= 0f)
            return;

        // s_severityField is null if the field was renamed in a future patch; fall back gracefully.
        if (s_severityField == null)
        {
            ApplyHealPulseFallback(pawn, healPoints);
            return;
        }

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

        float per = totalSev > 0f ? 0f : healPoints / injuries.Count;

        for (int i = 0; i < injuries.Count; i++)
        {
            Hediff_Injury inj = injuries[i];
            float amount = totalSev > 0f ? healPoints * (inj.Severity / totalSev) : per;
            float newSev = Mathf.Max(0f, inj.Severity - amount);
            // Write directly to the backing field — no Notify_HediffChanged, no setter auto-removal.
            s_severityField.SetValue(inj, newSev);
        }

        // Refresh hediff-set caches so ShouldRemove is evaluated correctly next health tick.
        pawn.health.hediffSet.DirtyCache();
    }

    /// <summary>
    /// Like <see cref="ApplyHealPulse"/> but only distributes healing across injuries that pass <paramref name="includeInjury"/>.
    /// </summary>
    public static void ApplyHealPulseToInjuries(Pawn pawn, float healPoints, Predicate<Hediff_Injury> includeInjury)
    {
        if (pawn?.health?.hediffSet == null || healPoints <= 0f || includeInjury == null)
            return;

        if (s_severityField == null)
        {
            ApplyHealPulseToInjuriesFallback(pawn, healPoints, includeInjury);
            return;
        }

        List<Hediff_Injury> injuries = new List<Hediff_Injury>();
        foreach (Hediff h in pawn.health.hediffSet.hediffs)
        {
            if (h is Hediff_Injury inj && !inj.IsPermanent() && includeInjury(inj))
                injuries.Add(inj);
        }

        if (injuries.Count == 0)
            return;

        float totalSev = 0f;
        for (int i = 0; i < injuries.Count; i++)
            totalSev += injuries[i].Severity;

        float per = totalSev > 0f ? 0f : healPoints / injuries.Count;

        for (int i = 0; i < injuries.Count; i++)
        {
            Hediff_Injury inj = injuries[i];
            float amount = totalSev > 0f ? healPoints * (inj.Severity / totalSev) : per;
            float newSev = Mathf.Max(0f, inj.Severity - amount);
            s_severityField.SetValue(inj, newSev);
        }

        pawn.health.hediffSet.DirtyCache();
    }

    private static void ApplyHealPulseToInjuriesFallback(Pawn pawn, float healPoints, Predicate<Hediff_Injury> includeInjury)
    {
        List<Hediff_Injury> injuries = new List<Hediff_Injury>();
        foreach (Hediff h in pawn.health.hediffSet.hediffs)
        {
            if (h is Hediff_Injury inj && !inj.IsPermanent() && includeInjury(inj))
                injuries.Add(inj);
        }

        if (injuries.Count == 0)
            return;

        float totalSev = 0f;
        for (int i = 0; i < injuries.Count; i++)
            totalSev += injuries[i].Severity;

        float per = totalSev > 0f ? 0f : healPoints / injuries.Count;
        for (int i = 0; i < injuries.Count; i++)
        {
            Hediff_Injury inj = injuries[i];
            float amount = totalSev > 0f ? healPoints * (inj.Severity / totalSev) : per;
            inj.Severity -= amount;
        }
    }

    /// <summary>Fallback used only if the severity backing-field reflection fails (future-proof guard).</summary>
    private static void ApplyHealPulseFallback(Pawn pawn, float healPoints)
    {
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

        float per = totalSev > 0f ? 0f : healPoints / injuries.Count;
        for (int i = 0; i < injuries.Count; i++)
        {
            Hediff_Injury inj = injuries[i];
            float amount = totalSev > 0f ? healPoints * (inj.Severity / totalSev) : per;
            inj.Severity -= amount;
        }
    }

    public static float DeathrestHealEfficiency(Pawn p)
    {
        Gene_Deathrest gene = p?.genes?.GetFirstGeneOfType<Gene_Deathrest>();
        return gene?.DeathrestEfficiency ?? 1f;
    }
}
