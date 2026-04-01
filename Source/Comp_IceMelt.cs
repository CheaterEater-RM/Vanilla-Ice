using Verse;

namespace VanillaIce
{
    public class CompProperties_IceMelt : CompProperties
    {
        public CompProperties_IceMelt()
        {
            compClass = typeof(Comp_IceMelt);
        }
    }

    /// <summary>
    /// Ice blocks absorb heat from their surroundings as they melt.
    /// When the cell temperature is above MeltThreshold (0 °C), some blocks
    /// melt each CompTickRare interval, pushing cold into the room via the
    /// same GenTemperature.PushHeat API used by vanilla coolers.
    ///
    /// ColdEnergyPerBlock is read from VanillaIceSettings so players can tune it.
    /// TempDropPerBlock is fixed: each 4 °C above freezing causes one block to melt.
    /// </summary>
    public class Comp_IceMelt : Comp_MeltableBase
    {
        private const float TempDropPerBlock = 4f;

        protected override float TempDropPerUnit => TempDropPerBlock;

        protected override float ColdEnergyPerUnit => GetConfiguredColdEnergy(VanillaIceMod.Settings?.ColdEnergyPerBlock
                                                                              ?? VanillaIceSettings.DefaultColdEnergyPerBlock);

        protected override float DefaultColdEnergyPerUnit => VanillaIceSettings.DefaultColdEnergyPerBlock;
    }
}
