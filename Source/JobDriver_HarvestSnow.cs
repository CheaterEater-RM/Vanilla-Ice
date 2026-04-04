using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaIce
{
    public class JobDriver_HarvestSnow : JobDriver
    {
        private const TargetIndex HarvestCellInd = TargetIndex.A;
        private const float BaseTicks     = 1000f;
        private const float SnowPerHarvest = 0.25f;

        private IntVec3 HarvestCell => job.GetTarget(HarvestCellInd).Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
            => pawn.Reserve(HarvestCell, job, 1, -1, null, errorOnFailed);

        private static int RoundRandom(float value)
        {
            int floor = Mathf.FloorToInt(value);
            float frac = value - floor;
            return floor + (Rand.Value < frac ? 1 : 0);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() =>
            {
                IntVec3 c   = HarvestCell;
                Map     map = pawn.Map;
                return !HarvestJobUtility.HasAllowedSnowHarvestZone(map, c);
            });

            yield return Toils_Goto.GotoCell(HarvestCellInd, PathEndMode.Touch);

            Toil carve = ToilMaker.MakeToil("ClearSnow");
            carve.defaultCompleteMode = ToilCompleteMode.Delay;
            carve.defaultDuration = Mathf.Max(
                100,
                Mathf.RoundToInt(BaseTicks / Mathf.Max(pawn.GetStatValue(StatDefOf.MiningSpeed), 0.01f)));
            carve.WithProgressBarToilDelay(HarvestCellInd);
            carve.tickAction = () => pawn.skills?.Learn(SkillDefOf.Mining, 0.07f);
            yield return carve;

            Toil complete = ToilMaker.MakeToil("CompleteSnowHarvest");
            complete.initAction = () =>
            {
                IntVec3 c   = HarvestCell;
                Map     map = pawn.Map;
                float depth = c.GetSnowDepth(map);
                if (depth < Zone_SnowHarvest.MinHarvestDepth) return;

                map.snowGrid.SetDepth(c, Mathf.Max(0f, depth - SnowPerHarvest));

                float baseYield   = VanillaIceMod.Settings?.SnowballsPerHarvest
                                    ?? VanillaIceSettings.DefaultSnowballsPerHarvest;
                float miningYield = pawn.GetStatValue(StatDefOf.MiningYield);
                int yield         = RoundRandom(baseYield * miningYield);
                if (yield < 1) yield = 1;

                Thing snowballs = ThingMaker.MakeThing(VanillaIceDefOf.VanillaIce_Snowball);
                snowballs.stackCount = yield;

                if (!GenPlace.TryPlaceThing(snowballs, c, map, ThingPlaceMode.Near))
                {
                    if (!GenPlace.TryPlaceThing(snowballs, pawn.Position, map, ThingPlaceMode.Near))
                    {
                        Log.Warning($"[VanillaIce] Failed to place {yield} snowball(s) from {c} for {pawn.LabelShort}.");
                        snowballs.Destroy(DestroyMode.Vanish);
                    }
                }
                pawn.records.Increment(RecordDefOf.CellsMined);
            };
            complete.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return complete;
        }
    }
}
