using Verse;

namespace VanillaIce
{
    public class CompProperties_SnowballMelt : CompProperties
    {
        public CompProperties_SnowballMelt() { compClass = typeof(Comp_SnowballMelt); }
    }

    public class Comp_SnowballMelt : Comp_MeltableBase
    {
        private const float AdditionalBallTemperatureStep = 4f;

        protected override float AdditionalUnitTemperatureStep => AdditionalBallTemperatureStep;

        protected override float ColdEnergyPerUnit => GetConfiguredColdEnergy(VanillaIceMod.Settings?.SnowballColdEnergy
                                                                              ?? VanillaIceSettings.DefaultSnowballColdEnergy);

        protected override float DefaultColdEnergyPerUnit => VanillaIceSettings.DefaultSnowballColdEnergy;
    }
}
