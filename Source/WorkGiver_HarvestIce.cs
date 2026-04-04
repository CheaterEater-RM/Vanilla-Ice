using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaIce
{
    public class WorkGiver_HarvestIce : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
            => ThingRequest.ForUndefined();

        public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
            => Enumerable.Empty<IntVec3>();

        public override Job NonScanJob(Pawn pawn)
        {
            if (pawn.Map == null) return null;
            if (!HarvestJobUtility.AllowsIceHarvesting(pawn.Map, out _)) return null;

            Zone_IceHarvest bestZone = null;
            float bestDistSq = float.MaxValue;

            foreach (Zone z in pawn.Map.zoneManager.AllZones)
            {
                Zone_IceHarvest hz = z as Zone_IceHarvest;
                if (hz == null) continue;
                if (!hz.Allowed) continue;
                if (!hz.TryGetClosestHarvestableDistanceSquared(pawn.Position, out float minDistSq)) continue;

                if (minDistSq < bestDistSq)
                {
                    bestZone = hz;
                    bestDistSq = minDistSq;
                }
            }

            if (bestZone == null) return null;

            foreach (IntVec3 c in bestZone.HarvestableCellsInRandomOrder())
            {
                if (!pawn.CanReserveAndReach(c, PathEndMode.Touch, Danger.Some, 1, -1, null))
                    continue;

                return JobMaker.MakeJob(VanillaIceDefOf.VanillaIce_HarvestIce, c);
            }

            return null;
        }
    }
}
