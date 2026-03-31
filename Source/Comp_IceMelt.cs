using RimWorld;
using UnityEngine;
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
    public class Comp_IceMelt : ThingComp
    {
        // Each 4 °C above freezing causes one extra block to melt per interval.
        // Kept fixed — adjusting this would make the melt curve non-obvious to players.
        private const float TempDropPerBlock = 4f;
        private const float MeltThreshold    = 0f;

        public override void CompTickRare()
        {
            Map map = parent.Map;
            if (map == null) return;

            float temp = parent.Position.GetTemperature(map);
            if (temp <= MeltThreshold) return;

            // How many blocks melt this interval?
            // One block per TempDropPerBlock °C above freezing, capped to stack size.
            int blocksMelted = Mathf.CeilToInt(temp / TempDropPerBlock);
            blocksMelted = Mathf.Min(blocksMelted, parent.stackCount);

            // Read cooling power from settings (player-configurable).
            float coldEnergyPerBlock = VanillaIceMod.Settings?.ColdEnergyPerBlock
                                       ?? VanillaIceSettings.DefaultColdEnergyPerBlock;

            // Push cold into the room — negative energy = cooling,
            // using the same API as vanilla heaters and coolers.
            GenTemperature.PushHeat(parent, -(blocksMelted * coldEnergyPerBlock));

            parent.stackCount -= blocksMelted;
            if (parent.stackCount <= 0)
                parent.Destroy();
        }
    }
}
