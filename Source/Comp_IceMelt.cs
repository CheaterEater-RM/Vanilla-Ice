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
    /// Tuning constants (see DESIGN.md §4.3 for derivation):
    ///   ColdEnergyPerBlock  — energy units absorbed per block melted
    ///   TempDropPerBlock    — °C of room temp that triggers one extra block to melt
    ///   MeltThreshold       — °C below which no melting occurs
    /// </summary>
    public class Comp_IceMelt : ThingComp
    {
        // ---------------------------------------------------------------------------
        // Tuning
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Energy (in RimWorld's internal GEU units) absorbed per block melted.
        /// Vanilla cooler reference: ~−87.5 GEU per CompTickRare interval (250 ticks).
        /// At ColdEnergyPerBlock = 8, a stack of 10 blocks delivers ~80 GEU of cooling
        /// before fully melting — comparable to a brief cooler burst.
        /// </summary>
        private const float ColdEnergyPerBlock = 8f;

        /// <summary>
        /// Each degree of room temperature above the threshold triggers
        /// one additional block to melt per interval.
        /// </summary>
        private const float TempDropPerBlock = 2f;

        /// <summary>
        /// Blocks do not melt at or below this temperature (°C).
        /// 0 °C = freezing point of water.
        /// </summary>
        private const float MeltThreshold = 0f;

        // ---------------------------------------------------------------------------
        // CompTickRare — fires every 250 ticks
        // ---------------------------------------------------------------------------

        public override void CompTickRare()
        {
            // Graceful no-op if Odyssey is not active (ice terrain/FreezeManager not present)
            if (!ModsConfig.OdysseyActive) return;

            Map map = parent.Map;
            if (map == null) return;

            // Use vanilla room temperature — no custom thermal grid needed
            float temp = parent.Position.GetTemperature(map);
            if (temp <= MeltThreshold) return;

            // How many blocks melt this tick?
            // Each TempDropPerBlock °C above zero melts one block.
            // Clamp to the actual stack so we don't over-destroy.
            int blocksMelted = Mathf.CeilToInt(temp / TempDropPerBlock);
            blocksMelted = Mathf.Min(blocksMelted, parent.stackCount);

            // Push cold into the room — negative energy = cooling
            GenTemperature.PushHeat(parent, -(blocksMelted * ColdEnergyPerBlock));

            parent.stackCount -= blocksMelted;
            if (parent.stackCount <= 0)
                parent.Destroy();
        }
    }
}
