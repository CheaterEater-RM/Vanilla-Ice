using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

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

        public IEnumerable<IntVec3> HarvestableCells => cells.Where(IsHarvestable);

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
                toggleAction = () => { allowed = !allowed; }
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref allowed, "allowed", defaultValue: true);
        }

        public override string GetInspectString()
        {
            int count = HarvestableCells.Count();
            return base.GetInspectString()
                + "\n" + "VanillaIce_HarvestableCells".Translate() + ": " + count;
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
