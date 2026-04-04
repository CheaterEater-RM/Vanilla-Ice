using Verse;

namespace VanillaIce
{
    public class VanillaIceMapComponent : MapComponent
    {
        public VanillaIceMapComponent(Map map)
            : base(map)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            HarvestJobUtility.CleanupInvalidHarvestJobs(map);
        }
    }
}