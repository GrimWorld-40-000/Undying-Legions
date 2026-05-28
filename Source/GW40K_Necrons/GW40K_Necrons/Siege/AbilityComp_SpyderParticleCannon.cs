using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace GW40K_Necrons;

public class SpyderParticleCannonProperties : CompProperties_AbilityEffect
{
    public int   burstCount          = 3;
    public int   ticksBetweenShots   = 15;
    public float forcedMissRadius    = 3f;
    public string projectileDefName  = "GW_UL_SpyderCannonShell";

    public SpyderParticleCannonProperties() => compClass = typeof(AbilityComp_SpyderParticleCannon);
}

/// <summary>
/// Fires <see cref="SpyderParticleCannonProperties.burstCount"/> lobbed projectiles in quick
/// succession at or near the target cell. Each shot scatters within
/// <see cref="SpyderParticleCannonProperties.forcedMissRadius"/> cells of the aim point.
/// </summary>
public class AbilityComp_SpyderParticleCannon : CompAbilityEffect
{
    private new SpyderParticleCannonProperties Props => (SpyderParticleCannonProperties)props;

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        ThingDef projDef = DefDatabase<ThingDef>.GetNamedSilentFail(Props.projectileDefName);
        if (projDef == null)
        {
            Log.Warning($"[UndyingLegions] SpyderParticleCannon: projectile def '{Props.projectileDefName}' not found.");
            return;
        }

        Pawn caster = parent.pawn;
        Map  map    = caster.Map;
        if (map == null) return;

        SoundDef.Named("GW_Plasma_Gun_Sound")?.PlayOneShot(SoundInfo.InMap(new TargetInfo(caster.Position, map)));

        // Fire shot 1 immediately.
        FireShot(caster, map, projDef, ScatteredCell(target.Cell, Props.forcedMissRadius, map));

        // Queue remaining shots via Comp_SpyderBurstQueue so they fire with
        // ticksBetweenShots spacing instead of all launching in the same frame.
        Comp_SpyderBurstQueue queue = caster.TryGetComp<Comp_SpyderBurstQueue>();
        int now = Find.TickManager.TicksGame;
        for (int i = 1; i < Props.burstCount; i++)
        {
            IntVec3 aimCell = ScatteredCell(target.Cell, Props.forcedMissRadius, map);
            if (queue != null)
                queue.Enqueue(now + i * Props.ticksBetweenShots, aimCell, projDef);
            else
                FireShot(caster, map, projDef, aimCell); // fallback if comp missing
        }
    }

    private static void FireShot(Pawn caster, Map map, ThingDef projDef, IntVec3 aimCell)
    {
        Projectile proj = (Projectile)GenSpawn.Spawn(projDef, caster.Position, map);
        proj.Launch(caster, caster.DrawPos,
            new LocalTargetInfo(aimCell), new LocalTargetInfo(aimCell),
            ProjectileHitFlags.NonTargetWorld);
    }

    private static IntVec3 ScatteredCell(IntVec3 center, float radius, Map map)
    {
        if (radius <= 0f) return center;
        float angle = Rand.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist  = Rand.Range(0f, radius);
        IntVec3 cell = center + new IntVec3(
            Mathf.RoundToInt(Mathf.Cos(angle) * dist),
            0,
            Mathf.RoundToInt(Mathf.Sin(angle) * dist));
        return cell.InBounds(map) ? cell : center;
    }
}
