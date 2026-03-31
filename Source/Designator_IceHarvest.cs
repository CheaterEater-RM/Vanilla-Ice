using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaIce
{
    public class Designator_ZoneAdd_IceHarvest : Designator_ZoneAdd
    {
        protected override string NewZoneLabel => "VanillaIce_IceHarvestZone".Translate();

        public Designator_ZoneAdd_IceHarvest()
        {
            zoneTypeToPlace = typeof(Zone_IceHarvest);
            defaultLabel    = "VanillaIce_IceHarvestZone".Translate();
            defaultDesc     = "VanillaIce_DesignatorIceHarvestZoneDesc".Translate();
            // Fall back to the stockpile icon if no custom texture is present.
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_IceHarvest", false)
                ?? ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile", false)
                ?? TexButton.Add;
        }

        protected override Zone MakeNewZone()
            => new Zone_IceHarvest(Find.CurrentMap.zoneManager);
    }

    /// <summary>Expand variant shown in the zone's own gizmo bar.</summary>
    public class Designator_ZoneAdd_IceHarvest_Expand : Designator_ZoneAdd_IceHarvest
    {
        public Designator_ZoneAdd_IceHarvest_Expand()
        {
            defaultLabel = "DesignatorZoneExpand".Translate();
        }
    }
}
