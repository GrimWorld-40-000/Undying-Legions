using Verse;
using Verse.Sound;

namespace GW40K_Necrons;

/// <summary>
/// Projectile that plays its explosion sound ~1 second after impact rather than
/// immediately. The visual explosion and all damage apply instantly; only the
/// audio boom is held back, giving a "light-then-sound" feel for the heavy
/// anti-matter particle blasts.
///
/// Works by swapping in a silent stub SoundDef before calling base.Impact()
/// (which internally fires GenExplosion.DoExplosion and then destroys the
/// instance).  After the call the original sound def is restored on the shared
/// ThingDef and the real boom is queued in GameComponent_DelayedSound.
///
/// RimWorld is single-threaded for gameplay, so the brief ThingDef mutation is
/// safe even when multiple shells land on the same tick.
/// </summary>
public class Projectile_DelayedImpactSound : Projectile_Explosive
{
    // 20 ticks ≈ 0.33 real seconds at normal speed.
    private const int  DelayTicks      = 20;
    private const string SilentStubDef = "GW_UL_SoundStubSilent";

    protected override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        // Capture position and map NOW — base.Impact() calls Explode() which
        // calls this.Destroy(), invalidating Position/Map on the instance.
        IntVec3  pos = Position;
        Map      map = Map;

        // Determine which sound would normally play.
        // Projectile_Explosive.Explode() passes def.projectile.soundExplode to
        // GenExplosion.DoExplosion; if null, DoExplosion falls back to
        // damageDef.soundExplosion.
        SoundDef original   = def.projectile.soundExplode;
        SoundDef actualBoom = original ?? def.projectile.damageDef?.soundExplosion;

        // Swap in the silent stub so DoExplosion fires with no audio.
        def.projectile.soundExplode = SoundDef.Named(SilentStubDef);

        base.Impact(hitThing, blockedByShield); // visual + damage, no boom

        // Restore the shared ThingDef (instance is destroyed, def persists).
        def.projectile.soundExplode = original;

        // Queue the real boom.
        if (actualBoom != null && map != null)
            GameComponent_DelayedSound.Instance?.Schedule(actualBoom, pos, map, DelayTicks);
    }
}
