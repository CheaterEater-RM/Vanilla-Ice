using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaIce
{
    /// <summary>
    /// Designatable zone over frozen water. Colonists assigned to Mining will
    /// harvest harvestable cells automatically.
    ///
    /// Two ice types are supported:
    ///
    ///   TerrainDefOf.Ice      — vanilla "thick ice" present in all cold biomes;
    ///                           always available; yields blocks indefinitely
    ///                           (the terrain itself is not removed).
    ///
    ///   TerrainDefOf.ThinIce  — Odyssey DLC thin-ice overlay on water cells;
    ///                           harvesting removes the terrain, which can
    ///                           refreeze naturally via vanilla FreezeManager.
    ///
    /// Both types are blocked by a building on the cell.
    /// </summary>
    public class Zone_IceHarvest : Zone
    {
        private bool allowed = true;

        public bool Allowed => allowed;

        protected override Color NextZoneColor => new Color(0.55f, 0.85f, 1f, 0.12f);

        // Parameterless ctor required by the zone system
        public Zone_IceHarvest() { }

        public Zone_IceHarvest(ZoneManager zoneManager)
            : base("VanillaIce_IceHarvestZone".Translate(), zoneManager) { }

        /// <summary>
        /// Returns true if the cell contains harvestable ice of any kind.
        /// </summary>
        public bool IsHarvestable(IntVec3 c)
        {
            if (c.GetFirstBuilding(Map) != null) return false;

            TerrainDef terrain = c.GetTerrain(Map);

            // Vanilla thick ice — always harvestable, terrain is not removed
            if (terrain == TerrainDefOf.Ice) return true;

            // Odyssey thin ice — only present when DLC is active
            if (ModsConfig.OdysseyActive && terrain == TerrainDefOf.ThinIce) return true;

            return false;
        }

        /// <summary>
        /// Returns the harvest mode for a cell: ThickIce, ThinIce, or None.
        /// Used by the JobDriver to decide what to do on completion.
        /// </summary>
        public static IceHarvestMode GetHarvestMode(IntVec3 c, Map map)
        {
            TerrainDef terrain = c.GetTerrain(map);
            if (terrain == TerrainDefOf.Ice) return IceHarvestMode.ThickIce;
            if (ModsConfig.OdysseyActive && terrain == TerrainDefOf.ThinIce) return IceHarvestMode.ThinIce;
            return IceHarvestMode.None;
        }

        public IEnumerable<IntVec3> HarvestableCells
        {
            get
            {
                foreach (IntVec3 cell in cells)
                {
                    if (IsHarvestable(cell))
                        yield return cell;
                }
            }
        }

        public bool TryGetClosestHarvestableDistanceSquared(IntVec3 from, out float distanceSquared)
        {
            distanceSquared = float.MaxValue;
            bool found = false;

            foreach (IntVec3 cell in cells)
            {
                if (!IsHarvestable(cell)) continue;

                found = true;
                float candidate = from.DistanceToSquared(cell);
                if (candidate < distanceSquared)
                    distanceSquared = candidate;
            }

            return found;
        }

        public IEnumerable<IntVec3> HarvestableCellsInRandomOrder()
        {
            foreach (IntVec3 cell in cells.InRandomOrder())
            {
                if (IsHarvestable(cell))
                    yield return cell;
            }
        }

        public int HarvestableCellCount()
        {
            int count = 0;
            foreach (IntVec3 cell in cells)
            {
                if (IsHarvestable(cell))
                    count++;
            }
            return count;
        }

        public override IEnumerable<Gizmo> GetZoneAddGizmos()
        {
            yield return DesignatorUtility.FindAllowedDesignator<Designator_ZoneAdd_IceHarvest_Expand>();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;
            yield return new Command_Toggle
            {
                defaultLabel = "VanillaIce_CommandAllowHarvestIce".Translate(),
                defaultDesc  = "VanillaIce_CommandAllowHarvestIceDesc".Translate(),
                icon         = TexCommand.ForbidOff,
                isActive     = () => allowed,
                toggleAction = () => SetAllowed(!allowed)
            };
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn))
                yield return option;

            if (selPawn == null || selPawn.Drafted)
                yield break;

            Job job = HarvestJobUtility.TryMakeForcedIceHarvestJob(selPawn, this, out string disabledReason);
            if (job != null)
            {
                yield return new FloatMenuOption(
                    "PrioritizeGenericSimple".Translate("VanillaIce_HarvestIceLabel".Translate()),
                    () => selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc));
                yield break;
            }

            if (!disabledReason.NullOrEmpty())
            {
                yield return new FloatMenuOption(
                    "VanillaIce_CannotPrioritize".Translate().CapitalizeFirst() + ": " + disabledReason,
                    null);
            }
        }

        public override void Delete(bool playSound)
        {
            HarvestJobUtility.InterruptCurrentHarvestJobsForZone(Map, VanillaIceDefOf.VanillaIce_HarvestIce, this);
            base.Delete(playSound);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref allowed, "allowed", defaultValue: true);
        }

        public override string GetInspectString()
        {
            int count = HarvestableCellCount();
            return base.GetInspectString()
                + "\n" + "VanillaIce_HarvestableCells".Translate() + ": " + count;
        }

        private void SetAllowed(bool newAllowed)
        {
            if (allowed == newAllowed) return;

            allowed = newAllowed;
            if (!allowed)
                HarvestJobUtility.InterruptCurrentHarvestJobsForZone(Map, VanillaIceDefOf.VanillaIce_HarvestIce, this);
        }
    }

    /// <summary>
    /// Distinguishes how the job driver should complete a harvest.
    /// Ordinal positions must never be changed after release (save compat).
    /// </summary>
    public enum IceHarvestMode
    {
        None     = 0,
        ThickIce = 1,   // vanilla Ice terrain — never removed; infinite harvests
        ThinIce  = 2,   // Odyssey ThinIce temp terrain — removed on harvest
    }
}
