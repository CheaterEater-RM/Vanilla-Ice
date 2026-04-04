using UnityEngine;
using Verse;

namespace VanillaIce
{
    public class VanillaIceMapComponent : MapComponent
    {
        public const int DefaultPauseAtStockCount = 225;
        public const int DefaultResumeAtStockCount = 150;

        private bool iceStockLimitEnabled;
        private int icePauseAtStockCount = DefaultPauseAtStockCount;
        private int iceResumeAtStockCount = DefaultResumeAtStockCount;
        private bool icePausedForStock;

        private bool snowStockLimitEnabled;
        private int snowPauseAtStockCount = DefaultPauseAtStockCount;
        private int snowResumeAtStockCount = DefaultResumeAtStockCount;
        private bool snowPausedForStock;

        public bool IceStockLimitEnabled => iceStockLimitEnabled;
        public int IcePauseAtStockCount => icePauseAtStockCount;
        public int IceResumeAtStockCount => iceResumeAtStockCount;
        public bool IcePausedForStock => icePausedForStock;
        public int IceBlockCount => HarvestJobUtility.CountThingsOnMap(map, VanillaIceDefOf.VanillaIce_IceBlock);

        public bool SnowStockLimitEnabled => snowStockLimitEnabled;
        public int SnowPauseAtStockCount => snowPauseAtStockCount;
        public int SnowResumeAtStockCount => snowResumeAtStockCount;
        public bool SnowPausedForStock => snowPausedForStock;
        public int SnowballCount => HarvestJobUtility.CountThingsOnMap(map, VanillaIceDefOf.VanillaIce_Snowball);

        public VanillaIceMapComponent(Map map)
            : base(map)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            HarvestJobUtility.CleanupInvalidHarvestJobs(map);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref iceStockLimitEnabled, "iceStockLimitEnabled", defaultValue: false);
            Scribe_Values.Look(ref icePauseAtStockCount, "icePauseAtStockCount", DefaultPauseAtStockCount);
            Scribe_Values.Look(ref iceResumeAtStockCount, "iceResumeAtStockCount", DefaultResumeAtStockCount);
            Scribe_Values.Look(ref icePausedForStock, "icePausedForStock", defaultValue: false);

            Scribe_Values.Look(ref snowStockLimitEnabled, "snowStockLimitEnabled", defaultValue: false);
            Scribe_Values.Look(ref snowPauseAtStockCount, "snowPauseAtStockCount", DefaultPauseAtStockCount);
            Scribe_Values.Look(ref snowResumeAtStockCount, "snowResumeAtStockCount", DefaultResumeAtStockCount);
            Scribe_Values.Look(ref snowPausedForStock, "snowPausedForStock", defaultValue: false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ClampIceThresholds();
                ClampSnowThresholds();
            }
        }

        public bool IsIceHarvestingAllowed(out string disabledReason)
        {
            disabledReason = null;
            UpdateIcePausedForStock(IceBlockCount);

            if (icePausedForStock)
            {
                disabledReason = "VanillaIce_CannotHarvestIce_StockLimitReached".Translate(
                    IceBlockCount,
                    icePauseAtStockCount,
                    iceResumeAtStockCount);
                return false;
            }

            return true;
        }

        public bool IsSnowHarvestingAllowed(out string disabledReason)
        {
            disabledReason = null;
            UpdateSnowPausedForStock(SnowballCount);

            if (snowPausedForStock)
            {
                disabledReason = "VanillaIce_CannotHarvestSnow_StockLimitReached".Translate(
                    SnowballCount,
                    snowPauseAtStockCount,
                    snowResumeAtStockCount);
                return false;
            }

            return true;
        }

        public void SetIceStockLimitEnabled(bool enabled)
        {
            if (iceStockLimitEnabled == enabled) return;

            iceStockLimitEnabled = enabled;
            RefreshIcePauseState(interruptJobsIfPaused: true);
        }

        public void SetSnowStockLimitEnabled(bool enabled)
        {
            if (snowStockLimitEnabled == enabled) return;

            snowStockLimitEnabled = enabled;
            RefreshSnowPauseState(interruptJobsIfPaused: true);
        }

        public void SetIcePauseAtStockCount(int value)
        {
            icePauseAtStockCount = Mathf.Max(0, value);
            ClampIceThresholds();
            RefreshIcePauseState(interruptJobsIfPaused: true);
        }

        public void SetSnowPauseAtStockCount(int value)
        {
            snowPauseAtStockCount = Mathf.Max(0, value);
            ClampSnowThresholds();
            RefreshSnowPauseState(interruptJobsIfPaused: true);
        }

        public void SetIceResumeAtStockCount(int value)
        {
            iceResumeAtStockCount = Mathf.Max(0, value);
            ClampIceThresholds();
            RefreshIcePauseState(interruptJobsIfPaused: false);
        }

        public void SetSnowResumeAtStockCount(int value)
        {
            snowResumeAtStockCount = Mathf.Max(0, value);
            ClampSnowThresholds();
            RefreshSnowPauseState(interruptJobsIfPaused: false);
        }

        private void RefreshIcePauseState(bool interruptJobsIfPaused)
        {
            bool wasPaused = icePausedForStock;
            UpdateIcePausedForStock(IceBlockCount);

            if (interruptJobsIfPaused && !wasPaused && icePausedForStock)
                HarvestJobUtility.InterruptCurrentHarvestJobs(map, VanillaIceDefOf.VanillaIce_HarvestIce);
        }

        private void RefreshSnowPauseState(bool interruptJobsIfPaused)
        {
            bool wasPaused = snowPausedForStock;
            UpdateSnowPausedForStock(SnowballCount);

            if (interruptJobsIfPaused && !wasPaused && snowPausedForStock)
                HarvestJobUtility.InterruptCurrentHarvestJobs(map, VanillaIceDefOf.VanillaIce_HarvestSnow);
        }

        private void UpdateIcePausedForStock(int currentCount)
        {
            if (!iceStockLimitEnabled)
            {
                icePausedForStock = false;
                return;
            }

            if (currentCount >= icePauseAtStockCount)
                icePausedForStock = true;
            else if (currentCount <= iceResumeAtStockCount)
                icePausedForStock = false;
        }

        private void UpdateSnowPausedForStock(int currentCount)
        {
            if (!snowStockLimitEnabled)
            {
                snowPausedForStock = false;
                return;
            }

            if (currentCount >= snowPauseAtStockCount)
                snowPausedForStock = true;
            else if (currentCount <= snowResumeAtStockCount)
                snowPausedForStock = false;
        }

        private void ClampIceThresholds()
        {
            icePauseAtStockCount = Mathf.Max(0, icePauseAtStockCount);
            iceResumeAtStockCount = Mathf.Max(0, iceResumeAtStockCount);

            if (icePauseAtStockCount == 0)
            {
                iceResumeAtStockCount = 0;
                return;
            }

            if (iceResumeAtStockCount >= icePauseAtStockCount)
                iceResumeAtStockCount = icePauseAtStockCount - 1;
        }

        private void ClampSnowThresholds()
        {
            snowPauseAtStockCount = Mathf.Max(0, snowPauseAtStockCount);
            snowResumeAtStockCount = Mathf.Max(0, snowResumeAtStockCount);

            if (snowPauseAtStockCount == 0)
            {
                snowResumeAtStockCount = 0;
                return;
            }

            if (snowResumeAtStockCount >= snowPauseAtStockCount)
                snowResumeAtStockCount = snowPauseAtStockCount - 1;
        }
    }
}