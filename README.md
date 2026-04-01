# Vanilla Ice

A lightweight RimWorld mod (1.6 / Odyssey) that lets colonists harvest ice from frozen lakes and use the blocks to passively cool rooms.

## Features

- **Ice Harvest Zones** — Paint zones over thin ice. Colonists assigned to Mining will harvest harvestable cells automatically.
- **Ice Blocks** — Each harvest yields 2–5 blocks. Stack them in a cool room or use them as a makeshift cooler.
- **Passive cooling** — Ice blocks absorb heat as they melt via vanilla's `GenTemperature.PushHeat`, the same mechanism used by electric coolers. One stack won't replace a cooler, but several blocks in a small room can make a real difference.
- **Caravan melting** — Ice blocks and snowballs now melt in caravans when the caravan is above freezing, using outdoor world-tile temperature and a configurable stack-based melt rate.
- **Natural refreeze** — Harvested cells revert to open water. Vanilla's `FreezeManager` handles refreezing when temperatures drop again.

## Requirements

- RimWorld 1.6
- Odyssey DLC (thin ice terrain is an Odyssey feature). The mod loads safely without Odyssey but will have no in-game effect.

## Compatibility

No Harmony patches. The mod adds new zone/job/item types plus a world component for caravan melting, and does not modify vanilla methods. Safe to add or remove from existing saves.

## Author

CheaterEater
