using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaIce
{
    public class Zone_SnowHarvest : Zone
    {
        public const float MinHarvestDepth = 0.25f;

        private bool allowed = true;
        public bool Allowed => allowed;
        private VanillaIceMapComponent MapComponent => Map?.GetComponent<VanillaIceMapComponent>();
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
                toggleAction = () => SetAllowed(!allowed)
            };

            if (MapComponent != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "VanillaIce_CommandMaintainStockTarget".Translate(),
                    defaultDesc  = "VanillaIce_CommandMaintainStockTargetDesc".Translate(),
                    icon         = TexButton.Suspend,
                    action       = OpenStockConfigDialog
                };
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn))
                yield return option;

            if (selPawn == null || selPawn.Drafted)
                yield break;

            Job job = HarvestJobUtility.TryMakeForcedSnowHarvestJob(selPawn, this, out string disabledReason);
            if (job != null)
            {
                yield return new FloatMenuOption(
                    "PrioritizeGenericSimple".Translate("VanillaIce_HarvestSnowLabel".Translate()),
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
            HarvestJobUtility.InterruptCurrentHarvestJobsForZone(Map, VanillaIceDefOf.VanillaIce_HarvestSnow, this);
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
            string inspectString = base.GetInspectString()
                + "\n" + "VanillaIce_HarvestableCells".Translate() + ": " + count;

            VanillaIceMapComponent component = MapComponent;
            if (component == null)
                return inspectString;

            int stockCount = component.SnowballCount;

            inspectString += "\n" + "VanillaIce_MapStockCount".Translate() + ": " + stockCount;
            inspectString += "\n" + (component.SnowStockLimitEnabled
                ? (component.SnowPausedForStock
                    ? "VanillaIce_StockLimitPaused".Translate(stockCount, component.SnowPauseAtStockCount, component.SnowResumeAtStockCount)
                    : "VanillaIce_StockLimitActive".Translate(stockCount, component.SnowPauseAtStockCount, component.SnowResumeAtStockCount))
                : "VanillaIce_StockLimitDisabled".Translate());

            return inspectString;
        }

        private void SetAllowed(bool newAllowed)
        {
            if (allowed == newAllowed) return;

            allowed = newAllowed;
            if (!allowed)
                HarvestJobUtility.InterruptCurrentHarvestJobsForZone(Map, VanillaIceDefOf.VanillaIce_HarvestSnow, this);
        }

        private void OpenStockConfigDialog()
        {
            VanillaIceMapComponent component = MapComponent;
            if (component == null) return;

            Find.WindowStack.Add(new Dialog_HarvestStockConfig(
                "VanillaIce_CommandMaintainStockTarget",
                "VanillaIce_StockTargetConfigDescription",
                "VanillaIce_StockTargetEnabled",
                "VanillaIce_StockTargetEnabledDesc",
                "VanillaIce_StockTargetPauseField",
                "VanillaIce_StockTargetResumeField",
                () => component.SnowballCount,
                () => component.SnowStockLimitEnabled,
                component.SetSnowStockLimitEnabled,
                () => component.SnowPauseAtStockCount,
                component.SetSnowPauseAtStockCount,
                () => component.SnowResumeAtStockCount,
                component.SetSnowResumeAtStockCount));
        }
    }
}
