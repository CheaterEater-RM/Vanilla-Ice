using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaIce
{
    public class WorkGiver_HarvestSnow : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
            => ThingRequest.ForUndefined();

        public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
            => Enumerable.Empty<IntVec3>();

        public override Job NonScanJob(Pawn pawn)
        {
            Zone_SnowHarvest bestZone = null;
            float bestDistSq = float.MaxValue;

            foreach (Zone z in pawn.Map.zoneManager.AllZones)
            {
                Zone_SnowHarvest hz = z as Zone_SnowHarvest;
                if (hz == null) continue;
                if (!hz.Allowed) continue;
                if (!hz.HarvestableCells.Any()) continue;

                float minDistSq = float.MaxValue;
                foreach (IntVec3 cell in hz.HarvestableCells)
                {
                    float d = pawn.Position.DistanceToSquared(cell);
                    if (d < minDistSq) minDistSq = d;
                }
                if (minDistSq < bestDistSq)
                {
                    bestZone = hz;
                    bestDistSq = minDistSq;
                }
            }

            if (bestZone == null) return null;

            foreach (IntVec3 c in bestZone.HarvestableCells.InRandomOrder())
            {
                if (!pawn.CanReserveAndReach(c, PathEndMode.Touch, Danger.Some, 1, -1, null))
                    continue;
                return JobMaker.MakeJob(VanillaIceDefOf.VanillaIce_HarvestSnow, c);
            }

            return null;
        }
    }
}
