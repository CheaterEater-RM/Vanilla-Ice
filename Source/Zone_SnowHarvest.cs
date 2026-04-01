using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaIce
{
    public class Zone_SnowHarvest : Zone
    {
        public const float MinHarvestDepth = 0.25f;

        private bool allowed = true;
        public bool Allowed => allowed;
        protected override Color NextZoneColor => new Color(0.85f, 0.92f, 1f, 0.14f);

        public Zone_SnowHarvest() { }
        public Zone_SnowHarvest(ZoneManager zoneManager)
            : base("VanillaIce_SnowHarvestZone".Translate(), zoneManager) { }

        public bool IsHarvestable(IntVec3 c)
        {
            if (c.GetFirstBuilding(Map) != null) return false;
            return c.GetSnowDepth(Map) >= MinHarvestDepth;
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
            yield return DesignatorUtility.FindAllowedDesignator<Designator_ZoneAdd_SnowHarvest_Expand>();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;
            yield return new Command_Toggle
            {
                defaultLabel = "VanillaIce_CommandAllowHarvestSnow".Translate(),
                defaultDesc  = "VanillaIce_CommandAllowHarvestSnowDesc".Translate(),
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
            int count = HarvestableCellCount();
            return base.GetInspectString()
                + "\n" + "VanillaIce_HarvestableCells".Translate() + ": " + count;
        }
    }
}
