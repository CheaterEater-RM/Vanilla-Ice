# Vanilla Ice

A lightweight RimWorld mod for RimWorld 1.6 that lets colonists harvest ice and snow, then use the results to passively cool rooms and caravans.

## Features

- **Ice Harvest Zones** — Paint zones over frozen water. Colonists assigned to Mining will harvest harvestable cells automatically.
- **Ice Blocks** — Harvested ice becomes stackable ice blocks. Yield is configurable and scales with Mining Yield.
- **Snow Harvest Zones** — Harvest snow into stackable snowballs for lighter low-tech cooling.
- **Passive cooling** — Ice blocks absorb heat as they melt via vanilla's `GenTemperature.PushHeat`, the same mechanism used by electric coolers. One stack won't replace a cooler, but several blocks in a small room can make a real difference.
- **Caravan melting** — Ice blocks and snowballs now melt in caravans when the caravan is above freezing, using outdoor world-tile temperature and a configurable stack-based melt rate.
- **Natural refreeze** — Harvested cells revert to open water. Vanilla's `FreezeManager` handles refreezing when temperatures drop again.

## Requirements

- RimWorld 1.6
- Odyssey DLC is optional. Without Odyssey, thin-ice harvesting is unavailable, but thick ice and snow harvesting still work.

## Compatibility

No Harmony patches. The mod adds new zone/job/item types plus a world component for caravan melting, and does not modify vanilla methods. Safe to add or remove from existing saves.

## Author

CheaterEater
