using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaIce
{
    public class JobDriver_HarvestIce : JobDriver
    {
        private const TargetIndex HarvestCellInd = TargetIndex.A;

        // Yield per harvest in blocks.
        // ThickIce (vanilla, infinite source): small yield per cut feels appropriate
        // ThinIce (Odyssey, cell is consumed): slightly more generous to compensate
        private const int ThickIceYieldMin = 1;
        private const int ThickIceYieldMax = 3;
        private const int ThinIceYieldMin  = 2;
        private const int ThinIceYieldMax  = 5;

        // Base work duration in ticks, scaled by MiningSpeed.
        private const float BaseTicks = 2000f;

        private IntVec3 HarvestCell => job.GetTarget(HarvestCellInd).Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
            => pawn.Reserve(HarvestCell, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail if the cell is no longer harvestable (melted, terrain changed, etc.)
            // or the zone was deleted/toggled off.
            this.FailOn(() =>
            {
                IntVec3 c = HarvestCell;
                Map map = pawn.Map;

                if (Zone_IceHarvest.GetHarvestMode(c, map) == IceHarvestMode.None) return true;

                foreach (Zone z in map.zoneManager.AllZones)
                {
                    Zone_IceHarvest hz = z as Zone_IceHarvest;
                    if (hz != null && hz.Allowed && hz.ContainsCell(c))
                        return false; // zone still valid
                }
                return true; // no valid zone
            });

            // Step 1: Walk to the cell
            yield return Toils_Goto.GotoCell(HarvestCellInd, PathEndMode.Touch);

            // Step 2: Carve ice — progress bar, scaled by MiningSpeed
            Toil carve = ToilMaker.MakeToil("CarveIce");
            carve.defaultCompleteMode = ToilCompleteMode.Delay;
            carve.defaultDuration = Mathf.Max(
                100,
                Mathf.RoundToInt(BaseTicks / Mathf.Max(pawn.GetStatValue(StatDefOf.MiningSpeed), 0.01f)));
            carve.WithProgressBarToilDelay(HarvestCellInd);
            carve.tickAction = () => pawn.skills?.Learn(SkillDefOf.Mining, 0.07f);
            yield return carve;

            // Step 3: Complete — spawn blocks; remove ThinIce if applicable
            Toil complete = ToilMaker.MakeToil("CompleteIceHarvest");
            complete.initAction = () =>
            {
                IntVec3 c = HarvestCell;
                Map map = pawn.Map;

                IceHarvestMode mode = Zone_IceHarvest.GetHarvestMode(c, map);
                if (mode == IceHarvestMode.None) return; // terrain changed during carve

                // For ThinIce: remove the terrain so water can refreeze naturally.
                // For ThickIce: terrain is left intact — it's an infinite source.
                if (mode == IceHarvestMode.ThinIce)
                    map.terrainGrid.RemoveTempTerrain(c);

                // Spawn ice blocks near the harvest cell
                int yield = (mode == IceHarvestMode.ThinIce)
                    ? Rand.RangeInclusive(ThinIceYieldMin, ThinIceYieldMax)
                    : Rand.RangeInclusive(ThickIceYieldMin, ThickIceYieldMax);

                Thing iceBlocks = ThingMaker.MakeThing(VanillaIceDefOf.VanillaIce_IceBlock);
                iceBlocks.stackCount = yield;

                if (!GenPlace.TryPlaceThing(iceBlocks, c, map, ThingPlaceMode.Near))
                {
                    if (!GenPlace.TryPlaceThing(iceBlocks, pawn.Position, map, ThingPlaceMode.Near))
                        Log.Warning($"[VanillaIce] Failed to place {yield} ice block(s) from {c} for {pawn.LabelShort}.");
                }

                pawn.records.Increment(RecordDefOf.CellsMined);
            };
            complete.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return complete;
        }
    }
}
