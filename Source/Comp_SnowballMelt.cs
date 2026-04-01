using Verse;

namespace VanillaIce
{
    public class CompProperties_SnowballMelt : CompProperties
    {
        public CompProperties_SnowballMelt() { compClass = typeof(Comp_SnowballMelt); }
    }

    public class Comp_SnowballMelt : Comp_MeltableBase
    {
        private const float TempDropPerBall = 4f;

        protected override float TempDropPerUnit => TempDropPerBall;

        protected override float ColdEnergyPerUnit => GetConfiguredColdEnergy(VanillaIceMod.Settings?.SnowballColdEnergy
                                                                              ?? VanillaIceSettings.DefaultSnowballColdEnergy);

        protected override float DefaultColdEnergyPerUnit => VanillaIceSettings.DefaultSnowballColdEnergy;
    }
}
