using UnityEngine;
using Verse;

namespace VanillaIce
{
    public abstract class Comp_MeltableBase : ThingComp
    {
        public const float MeltThreshold = 0f;
        private const float ReferenceCaravanTemperature = 25f;
        private const float TicksPerDay = 60000f;

        private float caravanMeltProgress;

        protected abstract float TempDropPerUnit { get; }

        protected abstract float ColdEnergyPerUnit { get; }

        protected abstract float DefaultColdEnergyPerUnit { get; }

        public override void CompTickRare()
        {
            Map map = parent.Map;
            if (map == null) return;

            float temp = parent.Position.GetTemperature(map);
            if (temp <= MeltThreshold) return;

            int unitsMelted = Mathf.CeilToInt(temp / TempDropPerUnit);
            unitsMelted = Mathf.Min(unitsMelted, parent.stackCount);
            if (unitsMelted <= 0) return;

            GenTemperature.PushHeat(parent, -(unitsMelted * ColdEnergyPerUnit));
            RemoveUnits(unitsMelted);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref caravanMeltProgress, "caravanMeltProgress", 0f);
        }

        public override void PostSplitOff(Thing piece)
        {
            base.PostSplitOff(piece);

            Comp_MeltableBase pieceComp = piece.TryGetComp<Comp_MeltableBase>();
            if (pieceComp == null) return;

            int totalCount = parent.stackCount + piece.stackCount;
            if (totalCount <= 0 || caravanMeltProgress <= 0f) return;

            float pieceProgress = caravanMeltProgress * piece.stackCount / totalCount;
            pieceComp.caravanMeltProgress += pieceProgress;
            caravanMeltProgress -= pieceProgress;
        }

        public override void PreAbsorbStack(Thing otherStack, int count)
        {
            base.PreAbsorbStack(otherStack, count);

            Comp_MeltableBase otherComp = otherStack.TryGetComp<Comp_MeltableBase>();
            if (otherComp == null || otherStack.stackCount <= 0 || count <= 0) return;

            float transferred = otherComp.caravanMeltProgress * count / otherStack.stackCount;
            caravanMeltProgress += transferred;
            otherComp.caravanMeltProgress -= transferred;
        }

        public void ApplyCaravanMelt(float temperature, int intervalTicks, float fullStackFractionPerDayAt25C)
        {
            if (parent.Destroyed || temperature <= MeltThreshold || intervalTicks <= 0 || fullStackFractionPerDayAt25C <= 0f)
                return;

            int stackCount = parent.stackCount;
            if (stackCount <= 0) return;

            float stackLimit = Mathf.Max(1f, parent.def.stackLimit);
            float normalizedStack = Mathf.Clamp01(stackCount / stackLimit);
            float surfaceScale = Mathf.Pow(normalizedStack, 2f / 3f);
            float temperatureScale = temperature / ReferenceCaravanTemperature;
            float fullStackDailyUnits = fullStackFractionPerDayAt25C * stackLimit;
            float unitsThisInterval = fullStackDailyUnits * surfaceScale * temperatureScale
                                      * intervalTicks / TicksPerDay;

            if (unitsThisInterval <= 0f) return;

            caravanMeltProgress += unitsThisInterval;

            int wholeUnits = Mathf.FloorToInt(caravanMeltProgress);
            if (wholeUnits <= 0) return;

            caravanMeltProgress -= wholeUnits;
            RemoveUnits(wholeUnits);
        }

        protected float GetConfiguredColdEnergy(float configuredValue)
        {
            return configuredValue > 0f ? configuredValue : DefaultColdEnergyPerUnit;
        }

        private void RemoveUnits(int count)
        {
            int removed = Mathf.Min(count, parent.stackCount);
            if (removed <= 0) return;

            parent.stackCount -= removed;
            if (parent.stackCount > 0) return;

            caravanMeltProgress = 0f;
            parent.Destroy();
        }
    }
}