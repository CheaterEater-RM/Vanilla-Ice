using UnityEngine;
using Verse;

namespace VanillaIce
{
    /// <summary>
    /// Mod entry point. Wires up settings persistence.
    /// Keep this class free of Def references — it fires before Defs are loaded.
    /// </summary>
    public class VanillaIceMod : Mod
    {
        public static VanillaIceSettings Settings;

        public VanillaIceMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<VanillaIceSettings>();
        }

        public override string SettingsCategory() => "VanillaIce_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            // --- Harvesting ---
            list.Label("VanillaIce_Settings_HarvestHeader".Translate());
            list.GapLine();

            // Base blocks per harvest action
            list.Label("VanillaIce_Settings_BlocksPerHarvest".Translate(
                Settings.BlocksPerHarvest.ToString("F1")));
            Settings.BlocksPerHarvest = list.Slider(Settings.BlocksPerHarvest, 1f, 50f);
            Settings.BlocksPerHarvest = Mathf.Round(Settings.BlocksPerHarvest); // snap to whole number

            list.Gap();

            // --- Cooling ---
            list.Label("VanillaIce_Settings_CoolingHeader".Translate());
            list.GapLine();

            // Cold energy per block
            list.Label("VanillaIce_Settings_ColdEnergyPerBlock".Translate(
                Settings.ColdEnergyPerBlock.ToString("F0")));
            Settings.ColdEnergyPerBlock = list.Slider(Settings.ColdEnergyPerBlock, 10f, 1000f);
            Settings.ColdEnergyPerBlock = Mathf.Round(Settings.ColdEnergyPerBlock / 10f) * 10f; // snap to 10

            list.Gap();
            list.Label("VanillaIce_Settings_SnowHarvestHeader".Translate());
            list.GapLine();
            list.Label("VanillaIce_Settings_SnowballsPerHarvest".Translate(
                Settings.SnowballsPerHarvest.ToString("F1")));
            Settings.SnowballsPerHarvest = list.Slider(Settings.SnowballsPerHarvest, 1f, 50f);
            Settings.SnowballsPerHarvest = Mathf.Round(Settings.SnowballsPerHarvest);

            list.Gap();
            list.Label("VanillaIce_Settings_SnowballColdEnergy".Translate(
                Settings.SnowballColdEnergy.ToString("F0")));
            Settings.SnowballColdEnergy = list.Slider(Settings.SnowballColdEnergy, 10f, 500f);
            Settings.SnowballColdEnergy = Mathf.Round(Settings.SnowballColdEnergy / 10f) * 10f;

            list.Gap();
            if (list.ButtonText("VanillaIce_Settings_ResetDefaults".Translate()))
                Settings.ResetToDefaults();

            list.End();
            base.DoSettingsWindowContents(inRect);
        }
    }

    public class VanillaIceSettings : ModSettings
    {
        // --- Defaults ---
        // A single harvest action yields this many blocks (before MiningYield stat).
        // Fractional part is handled probabilistically: 5.0 → always 5; 5.5 → 5 + 50% chance of +1.
        public const float DefaultBlocksPerHarvest = 10f;

        // Energy (GEU) absorbed per block melted per CompTickRare interval.
        // Reference: vanilla cooler ≈ 87.5 GEU/interval.
        public const float DefaultColdEnergyPerBlock  = 200f;
        public const float DefaultSnowballsPerHarvest = 10f;
        public const float DefaultSnowballColdEnergy  = 100f;

        // --- Live values ---
        public float BlocksPerHarvest    = DefaultBlocksPerHarvest;
        public float ColdEnergyPerBlock  = DefaultColdEnergyPerBlock;
        public float SnowballsPerHarvest = DefaultSnowballsPerHarvest;
        public float SnowballColdEnergy  = DefaultSnowballColdEnergy;

        public void ResetToDefaults()
        {
            BlocksPerHarvest    = DefaultBlocksPerHarvest;
            ColdEnergyPerBlock  = DefaultColdEnergyPerBlock;
            SnowballsPerHarvest = DefaultSnowballsPerHarvest;
            SnowballColdEnergy  = DefaultSnowballColdEnergy;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref BlocksPerHarvest,    "blocksPerHarvest",    DefaultBlocksPerHarvest);
            Scribe_Values.Look(ref ColdEnergyPerBlock,  "coldEnergyPerBlock",  DefaultColdEnergyPerBlock);
            Scribe_Values.Look(ref SnowballsPerHarvest, "snowballsPerHarvest", DefaultSnowballsPerHarvest);
            Scribe_Values.Look(ref SnowballColdEnergy,  "snowballColdEnergy",  DefaultSnowballColdEnergy);
            base.ExposeData();
        }
    }
}
