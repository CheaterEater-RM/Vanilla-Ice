using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaIce
{
    internal static class HarvestJobUtility
    {
        public static Job TryMakeForcedIceHarvestJob(Pawn pawn, Zone_IceHarvest zone, out string disabledReason)
            => TryMakeForcedHarvestJob(
                pawn,
                zone,
                zone.HarvestableCells,
                VanillaIceDefOf.VanillaIce_HarvestIce,
                "VanillaIce_CannotHarvestIce_NoHarvestableCells",
                out disabledReason);

        public static Job TryMakeForcedSnowHarvestJob(Pawn pawn, Zone_SnowHarvest zone, out string disabledReason)
            => TryMakeForcedHarvestJob(
                pawn,
                zone,
                zone.HarvestableCells,
                VanillaIceDefOf.VanillaIce_HarvestSnow,
                "VanillaIce_CannotHarvestSnow_NoHarvestableCells",
                out disabledReason);

        public static void InterruptCurrentHarvestJobsForZone(Map map, JobDef jobDef, Zone zone)
        {
            if (map?.mapPawns == null || jobDef == null || zone == null) return;

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                Job currentJob = pawn.CurJob;
                if (currentJob == null || currentJob.def != jobDef) continue;

                IntVec3 targetCell = currentJob.GetTarget(TargetIndex.A).Cell;
                if (!targetCell.IsValid || !zone.ContainsCell(targetCell)) continue;

                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        public static void CleanupInvalidHarvestJobs(Map map)
        {
            if (map?.mapPawns == null) return;

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                Job currentJob = pawn.CurJob;
                if (currentJob == null) continue;

                if (!IsStillValidHarvestJob(pawn, currentJob))
                    pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        public static bool HasAllowedIceHarvestZone(Map map, IntVec3 cell)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map)) return false;
            if (Zone_IceHarvest.GetHarvestMode(cell, map) == IceHarvestMode.None) return false;

            foreach (Zone zone in map.zoneManager.AllZones)
            {
                if (zone is Zone_IceHarvest iceZone && iceZone.Allowed && iceZone.ContainsCell(cell))
                    return true;
            }

            return false;
        }

        public static bool HasAllowedSnowHarvestZone(Map map, IntVec3 cell)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map)) return false;
            if (cell.GetSnowDepth(map) < Zone_SnowHarvest.MinHarvestDepth) return false;

            foreach (Zone zone in map.zoneManager.AllZones)
            {
                if (zone is Zone_SnowHarvest snowZone && snowZone.Allowed && snowZone.ContainsCell(cell))
                    return true;
            }

            return false;
        }

        private static Job TryMakeForcedHarvestJob(
            Pawn pawn,
            Zone zone,
            System.Collections.Generic.IEnumerable<IntVec3> harvestableCells,
            JobDef jobDef,
            string noHarvestableReasonKey,
            out string disabledReason)
        {
            disabledReason = null;

            if (pawn == null || zone?.Map == null || pawn.Map != zone.Map)
            {
                disabledReason = "VanillaIce_CannotHarvest_NotOnMap".Translate();
                return null;
            }

            if (pawn.Drafted)
            {
                disabledReason = "VanillaIce_CannotHarvest_Drafted".Translate();
                return null;
            }

            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Mining))
            {
                disabledReason = "CannotPrioritizeWorkTypeDisabled".Translate(WorkTypeDefOf.Mining.gerundLabel);
                return null;
            }

            if (pawn.health?.capacities == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                disabledReason = "IncapableOfCapacity".Translate(PawnCapacityDefOf.Manipulation.label).CapitalizeFirst();
                return null;
            }

            bool anyHarvestable = false;
            IntVec3 bestCell = IntVec3.Invalid;
            float bestDistanceSquared = float.MaxValue;

            foreach (IntVec3 cell in harvestableCells)
            {
                anyHarvestable = true;

                if (cell.IsForbidden(pawn)) continue;
                if (!pawn.CanReserveAndReach(cell, PathEndMode.Touch, Danger.Some, 1, -1, null)) continue;

                float distanceSquared = pawn.Position.DistanceToSquared(cell);
                if (distanceSquared >= bestDistanceSquared) continue;

                bestCell = cell;
                bestDistanceSquared = distanceSquared;
            }

            if (!anyHarvestable)
            {
                disabledReason = noHarvestableReasonKey.Translate();
                return null;
            }

            if (!bestCell.IsValid)
            {
                disabledReason = "VanillaIce_CannotHarvest_NoReachableCell".Translate();
                return null;
            }

            Job job = JobMaker.MakeJob(jobDef, bestCell);
            job.playerForced = true;
            return job;
        }

        private static bool IsStillValidHarvestJob(Pawn pawn, Job job)
        {
            if (pawn?.Map == null || job == null) return true;

            if (job.def == VanillaIceDefOf.VanillaIce_HarvestIce)
                return HasAllowedIceHarvestZone(pawn.Map, job.GetTarget(TargetIndex.A).Cell);

            if (job.def == VanillaIceDefOf.VanillaIce_HarvestSnow)
                return HasAllowedSnowHarvestZone(pawn.Map, job.GetTarget(TargetIndex.A).Cell);

            return true;
        }
    }
}