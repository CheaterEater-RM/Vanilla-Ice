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

        // Base work duration in ticks, scaled by MiningSpeed.
        private const float BaseTicks = 2000f;

        private IntVec3 HarvestCell => job.GetTarget(HarvestCellInd).Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
            => pawn.Reserve(HarvestCell, job, 1, -1, null, errorOnFailed);

        /// <summary>
        /// Converts a floating-point expected yield into a deterministic integer
        /// using the fractional part as a probability for an extra block.
        ///
        /// Examples:
        ///   5.0 → always 5
        ///   5.5 → 5 with 50% chance of being 6
        ///   2.3 → 2 with 30% chance of being 3
        ///
        /// This is the same approach as vanilla's GenMath.RoundRandom but made explicit.
        /// </summary>
        private static int RoundRandom(float value)
        {
            int floor = Mathf.FloorToInt(value);
            float frac = value - floor;
            return floor + (Rand.Value < frac ? 1 : 0);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail if the cell is no longer harvestable or the zone was deleted/toggled off.
            this.FailOn(() =>
            {
                IntVec3 c  = HarvestCell;
                Map    map = pawn.Map;

                if (Zone_IceHarvest.GetHarvestMode(c, map) == IceHarvestMode.None) return true;

                foreach (Zone z in map.zoneManager.AllZones)
                {
                    Zone_IceHarvest hz = z as Zone_IceHarvest;
                    if (hz != null && hz.Allowed && hz.ContainsCell(c))
                        return false; // zone still valid
                }
                return true; // no valid zone found
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
                IntVec3 c  = HarvestCell;
                Map    map = pawn.Map;

                IceHarvestMode mode = Zone_IceHarvest.GetHarvestMode(c, map);
                if (mode == IceHarvestMode.None) return; // terrain changed during carve

                // ThinIce is consumed; ThickIce is an infinite source.
                if (mode == IceHarvestMode.ThinIce)
                    map.terrainGrid.RemoveTempTerrain(c);

                // Base yield from settings (player-configurable, default 5).
                float baseYield = VanillaIceMod.Settings?.BlocksPerHarvest
                                  ?? VanillaIceSettings.DefaultBlocksPerHarvest;

                // Scale by the pawn's MiningYield stat (1.0 = 100%; Mining skill raises this).
                // Using the same stat vanilla rock mining uses.
                float miningYield = pawn.GetStatValue(StatDefOf.MiningYield);
                float expected    = baseYield * miningYield;

                // Convert to an integer probabilistically so the fractional part isn't lost.
                // e.g. expected=5.7 → 5 with 70% chance of +1 → average stays at 5.7.
                int yield = RoundRandom(expected);
                if (yield < 1) yield = 1; // always drop at least one block for feedback

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
