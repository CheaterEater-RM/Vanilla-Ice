# Vanilla Ice — Design Document

A standalone spinoff of the ice-harvesting mechanics from delQ, running entirely on vanilla RimWorld systems (no custom thermal grid).

---

## Core Design Goals

- Pawns designate Ice Harvest Zones over harvestable frozen water cells.
- Harvesting supports vanilla thick ice and Odyssey thin ice.
- Harvesting yields configurable stacks of Ice Blocks or Snowballs, scaled by Mining Yield.
- Ice Blocks and Snowballs, when above 0 °C, melt on maps and in caravans.

---

## Key Architecture Decisions

### No Harmony patches
The mod adds new types only (zone, job, comp, designator). It does not patch any vanilla methods. This makes it trivially safe to add/remove from saves.

### Odyssey guard pattern
All code paths that read `ThinIce` terrain or call `RemoveTempTerrain` are guarded by `ModsConfig.OdysseyActive`. Without Odyssey, thin-ice harvesting is skipped cleanly while thick ice and snow harvesting remain available.

### Binary ice model
delQ tracked ice thickness (float, in metres). Vanilla ice is binary: a cell either has `ThinIce` (a temp-terrain) or it doesn't. One harvest = one cell cleared.

### Refreeze via FreezeManager
After `map.terrainGrid.RemoveTempTerrain(c)` the underlying water terrain is exposed. Vanilla's `FreezeManager` will re-apply `ThinIce` when the cell temperature drops below `WaterFreezeThreshold` (−7 °C). No custom logic needed.

### CompTickRare and caravan melt component
Ice blocks and snowballs use `tickerType = Rare` (250-tick interval) on maps, avoiding per-tick overhead for large stockpiles. Caravan melting is handled by a `WorldComponent`, using world-tile outdoor temperature and the same 250-tick cadence.

### Vanilla PushHeat for cooling
`GenTemperature.PushHeat(parent, negativeEnergy)` distributes cooling into the room using the same path as heaters and coolers. No custom heat spreading required.

---

## Tuning Constants (Comp_IceMelt)

See §4.3 of `Vanilla_Ice_delQ_Reference.md` for full derivation.

| Constant | Value | Meaning |
|---|---|---|
| `MeltThreshold` | 0 °C | No melting at or below freezing |
| `TempDropPerBlock` | 4 °C | Each 4 °C above zero causes one block to melt per interval |
| `ColdEnergyPerBlock` | 200 GEU | Default energy absorbed per melted block |

Reference: vanilla cooler ≈ −87.5 GEU per CompTickRare interval. Default values are intentionally stronger than a single cooler-interval and are player-tunable in settings.

Adjust `ColdEnergyPerBlock` upward if blocks feel too weak, downward if they trivially freeze rooms.

---

## Harvest Parameters

| Parameter | Value | Notes |
|---|---|---|
| Work duration (base) | 2000 ticks | Scaled by MiningSpeed |
| Skill XP per tick | 0.07 Mining | Same as delQ |
| Yield per cell | Configurable base yield | Defaults to 10, scales with Mining Yield, fractional amounts are rounded probabilistically |
| Failure condition | Cell no longer ThinIce | Handles melt-during-carve edge case |

---

## Files Not Needed (vs. delQ)

| delQ component | Reason omitted |
|---|---|
| `TemperatureGrid` | Vanilla `GenTemperature` used instead |
| `TuningDef` / `ThermalProps` | Constants hardcoded |
| `MapExtensions` | No grid extension needed |
| `HeatSourceRegistryDef` | No heat source simulation |
| `Setup.cs` / Harmony | No method patching |
| `Settings.cs` | Present; exposes harvest, cooling, and caravan melt tuning |
| `Patches_BuildingThermal.xml` | No thermal property injection |

---

## Save Compatibility

- **Add to existing save**: safe. New zone type, job, and item — no impact on existing saves.
- **Remove from existing save**: safe. Designations and jobs fail gracefully. Ice blocks in stockpiles will lose their comp and disappear as unknown items on next load, which is acceptable vanilla behaviour for removed mods.
