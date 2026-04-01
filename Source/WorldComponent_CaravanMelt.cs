using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace VanillaIce
{
    public class WorldComponent_CaravanMelt : WorldComponent
    {
        private const int CaravanMeltIntervalTicks = 250;

        public WorldComponent_CaravanMelt(World world) : base(world)
        {
        }

        public override void WorldComponentTick()
        {
            if (Find.TickManager.TicksGame % CaravanMeltIntervalTicks != 0) return;

            List<Caravan> caravans = Find.WorldObjects?.Caravans;
            if (caravans == null || caravans.Count == 0) return;

            float meltPerDayAt25C = VanillaIceMod.Settings?.CaravanStackMeltPerDayAt25C
                                    ?? VanillaIceSettings.DefaultCaravanStackMeltPerDayAt25C;
            if (meltPerDayAt25C <= 0f) return;

            TileTemperaturesComp temperatures = Find.World?.GetComponent<TileTemperaturesComp>();
            if (temperatures == null) return;

            for (int i = 0; i < caravans.Count; i++)
            {
                Caravan caravan = caravans[i];
                if (caravan == null || caravan.Destroyed || !caravan.Spawned) continue;

                ProcessCaravan(caravan, temperatures, meltPerDayAt25C);
            }
        }

        private static void ProcessCaravan(Caravan caravan, TileTemperaturesComp temperatures, float meltPerDayAt25C)
        {
            List<Thing> inventoryItems = CaravanInventoryUtility.AllInventoryItems(caravan);
            if (inventoryItems.Count == 0) return;

            float outdoorTemp = temperatures.GetOutdoorTemp(caravan.Tile);
            if (outdoorTemp <= Comp_MeltableBase.MeltThreshold) return;

            for (int i = 0; i < inventoryItems.Count; i++)
            {
                Thing item = inventoryItems[i];
                if (item == null || item.Destroyed) continue;

                Comp_MeltableBase meltComp = item.TryGetComp<Comp_MeltableBase>();
                if (meltComp == null) continue;

                meltComp.ApplyCaravanMelt(outdoorTemp, CaravanMeltIntervalTicks, meltPerDayAt25C);
            }
        }
    }
}