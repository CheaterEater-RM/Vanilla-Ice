# Vanilla Ice — Design Document

A standalone spinoff of the ice-harvesting mechanics from delQ, running entirely on vanilla RimWorld systems (no custom thermal grid).

---

## Core Design Goals

- Pawns designate Ice Harvest Zones over frozen lake cells (ThinIce terrain).
- Harvesting removes the ThinIce temp-terrain layer; vanilla `FreezeManager` handles natural refreezing.
- Harvesting yields Ice Blocks (2–5 per cell, randomised).
- Ice Blocks, when stored above 0 °C, absorb heat and cool the room as they melt.

---

## Key Architecture Decisions

### No Harmony patches
The mod adds new types only (zone, job, comp, designator). It does not patch any vanilla methods. This makes it trivially safe to add/remove from saves.

### Odyssey guard pattern
All code paths that read `ThinIce` terrain or call `RemoveTempTerrain` are guarded by `ModsConfig.OdysseyActive`. The mod loads cleanly without Odyssey and simply does nothing.

### Binary ice model
delQ tracked ice thickness (float, in metres). Vanilla ice is binary: a cell either has `ThinIce` (a temp-terrain) or it doesn't. One harvest = one cell cleared.

### Refreeze via FreezeManager
After `map.terrainGrid.RemoveTempTerrain(c)` the underlying water terrain is exposed. Vanilla's `FreezeManager` will re-apply `ThinIce` when the cell temperature drops below `WaterFreezeThreshold` (−7 °C). No custom logic needed.

### CompTickRare for Comp_IceMelt
Ice blocks use `tickerType = Rare` (250-tick interval), avoiding per-tick overhead for large stockpiles. The melt calculation is intentionally conservative so stockpiles don't vanish instantly at room temperature.

### Vanilla PushHeat for cooling
`GenTemperature.PushHeat(parent, negativeEnergy)` distributes cooling into the room using the same path as heaters and coolers. No custom heat spreading required.

---

## Tuning Constants (Comp_IceMelt)

See §4.3 of `Vanilla_Ice_delQ_Reference.md` for full derivation.

| Constant | Value | Meaning |
|---|---|---|
| `MeltThreshold` | 0 °C | No melting at or below freezing |
| `TempDropPerBlock` | 2 °C | Each 2 °C above zero causes one block to melt per interval |
| `ColdEnergyPerBlock` | 8 GEU | Energy absorbed per melted block |

Reference: vanilla cooler ≈ −87.5 GEU per CompTickRare interval. A stack of 10 blocks delivers ≈80 GEU total — one cooler-interval's worth of cooling spread across its entire melt lifetime.

Adjust `ColdEnergyPerBlock` upward if blocks feel too weak, downward if they trivially freeze rooms.

---

## Harvest Parameters

| Parameter | Value | Notes |
|---|---|---|
| Work duration (base) | 2000 ticks | Scaled by MiningSpeed |
| Skill XP per tick | 0.07 Mining | Same as delQ |
| Yield per cell | Rand(2, 5) blocks | Randomised; adjust range for balance |
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
| `Settings.cs` | No mod settings (add later if desired) |
| `Patches_BuildingThermal.xml` | No thermal property injection |

---

## Save Compatibility

- **Add to existing save**: safe. New zone type, job, and item — no impact on existing saves.
- **Remove from existing save**: safe. Designations and jobs fail gracefully. Ice blocks in stockpiles will lose their comp and disappear as unknown items on next load, which is acceptable vanilla behaviour for removed mods.
