using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaIce
{
    public class Designator_ZoneAdd_SnowHarvest : Designator_ZoneAdd
    {
        protected override string NewZoneLabel => "VanillaIce_SnowHarvestZone".Translate();

        public Designator_ZoneAdd_SnowHarvest()
        {
            zoneTypeToPlace = typeof(Zone_SnowHarvest);
            defaultLabel    = "VanillaIce_SnowHarvestZone".Translate();
            defaultDesc     = "VanillaIce_DesignatorSnowHarvestZoneDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_SnowHarvest", false)
                ?? ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile", false)
                ?? TexButton.Add;
        }

        protected override Zone MakeNewZone()
            => new Zone_SnowHarvest(Find.CurrentMap.zoneManager);
    }

    public class Designator_ZoneAdd_SnowHarvest_Expand : Designator_ZoneAdd_SnowHarvest
    {
        public Designator_ZoneAdd_SnowHarvest_Expand()
        {
            defaultLabel = "DesignatorZoneExpand".Translate();
        }
    }
}
