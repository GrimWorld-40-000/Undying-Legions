using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace GW40K_Necrons
{
    [StaticConstructorOnStartup]
    public class CorpseCompAdder
    {
        static CorpseCompAdder()
        {
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (thingDef.IsCorpse)
                {
                    thingDef.comps.Add(new CompProperties_Resurrectible());
                }
            }
        }
    }
}
