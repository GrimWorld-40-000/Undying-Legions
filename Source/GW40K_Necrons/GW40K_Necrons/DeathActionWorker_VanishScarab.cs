using Verse;
using Verse.AI.Group;

namespace GW40K_Necrons;

public class DeathActionProperties_VanishScarab : DeathActionProperties
{
    public DeathActionProperties_VanishScarab()
    {
        workerClass = typeof(DeathActionWorker_VanishScarab);
    }
}

public class DeathActionWorker_VanishScarab : DeathActionWorker
{
    public override void PawnDied(Corpse corpse, Lord prevLord)
    {
        if (corpse != null && !corpse.Destroyed)
            corpse.Destroy();
    }
}
