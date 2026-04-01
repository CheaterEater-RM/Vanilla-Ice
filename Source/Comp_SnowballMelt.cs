using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaIce
{
    public class CompProperties_SnowballMelt : CompProperties
    {
        public CompProperties_SnowballMelt() { compClass = typeof(Comp_SnowballMelt); }
    }

    public class Comp_SnowballMelt : ThingComp
    {
        private const float TempDropPerBall = 4f;
        private const float MeltThreshold   = 0f;

        public override void CompTickRare()
        {
            Map map = parent.Map;
            if (map == null) return;

            float temp = parent.Position.GetTemperature(map);
            if (temp <= MeltThreshold) return;

            int ballsMelted = Mathf.CeilToInt(temp / TempDropPerBall);
            ballsMelted = Mathf.Min(ballsMelted, parent.stackCount);

            float coldEnergyPerBall = VanillaIceMod.Settings?.SnowballColdEnergy
                                      ?? VanillaIceSettings.DefaultSnowballColdEnergy;

            GenTemperature.PushHeat(parent, -(ballsMelted * coldEnergyPerBall));

            parent.stackCount -= ballsMelted;
            if (parent.stackCount <= 0)
                parent.Destroy();
        }
    }
}
