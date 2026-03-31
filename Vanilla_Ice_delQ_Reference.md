# Vanilla Ice — delQ Spinoff Reference

A design document for extracting the delQ ice-harvesting and ice-block-cooling mechanics into a standalone mod that works with vanilla RimWorld (Odyssey, 1.6+) without any dependency on delQ's custom thermal grid.

---

## 1. Goal and Scope

**Game design to preserve:**
- Pawns can designate frozen lake cells in an Ice Harvest Zone.
- When ice is harvested, the terrain reverts to the underlying water (which can refreeze naturally).
- Harvesting yields Ice Block items.
- Ice Blocks, when placed in a warm environment (above 0 °C), absorb heat and cool the room as they melt.

**What changes from delQ:**
- No `TemperatureGrid`. All temperature reads use vanilla's `c.GetTemperature(map)`.
- No ice-thickness tracking. Vanilla ice is binary: a cell either has `ThinIce` terrain (temp terrain overlay) or it does not.
- No `TuningDef` singleton. Constants are hardcoded or placed in a simple XML-driven `StaticDef`.
- Harvesting removes the `ThinIce` temp-terrain via `map.terrainGrid.RemoveTempTerrain(cell)`, which lets vanilla's `FreezeManager` handle refreezing naturally.
- Cold push from melting blocks uses `GenTemperature.PushHeat(parent, negativeEnergy)` — the same API vanilla coolers use.

**DLC requirement:**  
`ThinIce` terrain and `FreezeManager` are only active when `ModsConfig.OdysseyActive` is true (1.6 Odyssey DLC). All harvestability checks and the job driver must guard against this.

---

## 2. Vanilla Systems Reference

### 2.1 FreezeManager (Verse)

`FreezeManager` runs per-cell steady effects. Key constants:

| Constant | Value | Meaning |
|---|---|---|
| `WaterFreezeThreshold` | −7 °C | Water begins freezing below this |
| `IceMeltThreshold` | +2 °C | Ice begins melting above this |
| `BaseWaterFreezeChance` | 0.003 | Per-tick freeze probability base |
| `BaseIceMeltChance` | 0.001 | Per-tick melt probability base |
| `FreezeRadius` | 2.9 cells | Radial spread of freeze/melt events |

**Freezing** (`DoWaterFreezing`): calls `map.terrainGrid.SetTempTerrain(cell, TerrainDefOf.ThinIce)`.  
**Melting** (`DoIceMelting`): calls `map.terrainGrid.RemoveTempTerrain(cell)`.

Both run only when `ModsConfig.OdysseyActive`.

### 2.2 Terrain API for Ice

```csharp
// Check if a cell is frozen
bool isThinIce = c.GetTerrain(map) == TerrainDefOf.ThinIce;

// Check if a cell can freeze (underlying terrain is water)
bool canFreeze = FreezeManager.CanEverFreeze(c, map); // checks TerrainDef.canFreeze

// Remove ice (revert to underfloor water) — triggers natural refreezing later
map.terrainGrid.RemoveTempTerrain(c);
```

### 2.3 Temperature API

```csharp
// Get vanilla cell temperature (°C)
float temp = c.GetTemperature(map);

// Push heat into the room a Thing is in (negative = cooling)
GenTemperature.PushHeat(Thing source, float energy);
```

`GenTemperature.PushHeat` distributes the energy into the room using vanilla's standard path — the same mechanism used by heaters, coolers, and campfires.

---

## 3. Class-by-Class Adaptation Plan

### 3.1 `Zone_IceHarvest`

**Source:** `delQ/Source/Zone_IceHarvest.cs`  
**Changes:** Minimal. Remove `TuningDef` reference; replace ice-thickness check with terrain check; add Odyssey guard.

```
IsHarvestable(IntVec3 c):
  OLD: grid.GetIceThickness(idx) >= minThickness
  NEW: ModsConfig.OdysseyActive
       && c.GetTerrain(Map) == TerrainDefOf.ThinIce
       && c.GetFirstBuilding(Map) == null
```

Everything else (`HarvestableCells`, gizmos, `ExposeData`, `GetInspectString`) carries over unchanged.

---

### 3.2 `WorkGiver_HarvestIce`

**Source:** `delQ/Source/WorkGiver_HarvestIce.cs`  
**Changes:** None. The WorkGiver has no delQ-specific dependencies; it only calls `zone.HarvestableCells` and `IceWorkDefOf.delQ_HarvestIce`. Rename the DefOf reference to match the new mod's defName.

Rename the job defName from `delQ_HarvestIce` to something like `IceHarvest_HarvestIce` (or whatever the new mod prefix is).

---

### 3.3 `JobDriver_HarvestIce`

**Source:** `delQ/Source/JobDriver_HarvestIce.cs`  
**Changes:** Moderate. Three areas to adapt:

**A. FailOn condition**
```
OLD: grid.GetIceThickness(idx) < minThickness - 0.005f
NEW: !ModsConfig.OdysseyActive
     || c.GetTerrain(map) != TerrainDefOf.ThinIce
     (also keep the zone validity check)
```

**B. Work duration**
```
OLD: baseTicks = TuningDef.Active?.iceHarvestBaseTicks ?? 2000f
NEW: const float BaseTicks = 2000f;  // hardcoded, or from a simple StaticDef
     (same MiningSpeed scaling logic)
```

**C. Complete toil — what to harvest**
```
OLD:
  Read ice thickness from grid.
  cut = Min(thickness, iceHarvestDepthPerCut = 0.20m)
  yield = Floor(cut * 100cm * yieldPerCm = 0.5) → typically 10 blocks
  grid.SetIceThickness(idx, remaining)

NEW:
  Ice is binary. One harvest = one cell cleared.
  yield = Rand.RangeInclusive(IceBlockYieldMin, IceBlockYieldMax)
          (suggested defaults: min=2, max=5)
  map.terrainGrid.RemoveTempTerrain(c)   ← removes ThinIce, water can refreeze
```

No `ThermalGrid` calls. The `records.Increment(RecordDefOf.CellsMined)` and skill XP lines carry over unchanged.

---

### 3.4 `Comp_IceMelt`

**Source:** `delQ/Source/Comp_IceMelt.cs`  
**Changes:** Significant. Replace delQ thermal grid calls with vanilla equivalents. Simplify the heat physics.

**Old flow:**
1. Read cell temp from `ThermalGrid`.
2. Compute latent heat from block mass × compression factor.
3. Compute `energyToAbsorb = temp × airCellHeatCapacity`.
4. Melt `CeilToInt(energyToAbsorb / latentPerBlock)` blocks.
5. Call `grid.EnqueueHeatPush(cellIndex, -coldPush)`.

**New flow:**
1. Read cell temp from `parent.Position.GetTemperature(parent.Map)`.
2. If `temp <= MeltThreshold` (suggest 0 °C): do nothing.
3. Compute how many blocks melt this interval using a simplified formula:
   - `energyAbsorbed = blocksMelted × ColdEnergyPerBlock`
   - `ColdEnergyPerBlock` is a tunable constant (see §4.3 for derivation).
4. Call `GenTemperature.PushHeat(parent, -(blocksMelted × ColdEnergyPerBlock))`.
5. Reduce `parent.stackCount` by `blocksMelted`; destroy if ≤ 0.

**Tick strategy:** Use `CompTickRare` (fires every 250 ticks) instead of a manual counter, to reduce overhead when many blocks are stockpiled.

```csharp
public override void CompTickRare()
{
    if (!ModsConfig.OdysseyActive) return;   // graceful no-op outside Odyssey
    float temp = parent.Position.GetTemperature(parent.Map);
    if (temp <= 0f) return;

    // Melt enough blocks to absorb the excess heat
    int blocksMelted = Mathf.CeilToInt(temp / TempDropPerBlock);
    blocksMelted = Mathf.Min(blocksMelted, parent.stackCount);

    GenTemperature.PushHeat(parent, -(blocksMelted * ColdEnergyPerBlock));

    parent.stackCount -= blocksMelted;
    if (parent.stackCount <= 0)
        parent.Destroy();
}
```

Where `TempDropPerBlock` and `ColdEnergyPerBlock` are derived below (§4.3).

---

### 3.5 `Designator_ZoneAdd_IceHarvest`

**Source:** `delQ/Source/Designator_IceHarvest.cs`  
**Changes:** None beyond namespace/label strings. The designator has no delQ thermal dependencies. Rename `Designator_ZoneAdd_IceHarvest_Expand` if desired.

---

### 3.6 `IceWorkDefOf`

**Source:** `delQ/Source/IceWorkDefOf.cs`  
**Changes:** Update defNames to match the new mod's prefix.

```csharp
[DefOf]
public static class IceHarvestDefOf
{
    public static JobDef IceHarvest_HarvestIce;
    public static ThingDef IceHarvest_IceBlock;
}
```

---

## 4. Constants and Tuning

### 4.1 Harvest Parameters

| Parameter | delQ source | Vanilla spinoff |
|---|---|---|
| Min ice to harvest | `iceHarvestMinThickness` = 0.20 m | Cell must be `ThinIce` (binary) |
| Work duration (base) | `iceHarvestBaseTicks` = 2000 | 2000 ticks (hardcoded) |
| Yield per harvest | thickness-based (≈10 blocks) | `Rand.RangeInclusive(2, 5)` |
| Refreeze | `grid.SetIceThickness` manages partial ice | `RemoveTempTerrain` → vanilla `FreezeManager` handles refreeze |

### 4.2 Ice Block ThingDef

Carry over from delQ mostly unchanged. Update the `comps` class reference.

| Property | Value |
|---|---|
| defName | `IceHarvest_IceBlock` |
| stackLimit | 75 |
| Mass | 5 kg |
| MaxHitPoints | 40 |
| Flammability | 0 |
| MarketValue | 0.5 |
| tickerType | `Rare` (changed from `Normal`) |
| comp | `CompProperties_IceMelt` (new namespace) |

Change `tickerType` from `Normal` to `Rare` to match the `CompTickRare` approach and avoid per-tick overhead.

### 4.3 Melt Cooling Derivation

Vanilla's standard cooler (in a ≈25-cell room) drops temperature by roughly 20–30 °C. A vanilla cooler pushes about −21 energy/second. At 60 ticks/second, that's −0.35 energy/tick, or −87.5 energy per `CompTickRare` interval (250 ticks).

For ice blocks we want cooling to feel meaningful but not overpowered. A useful tuning target: one stack of 10 ice blocks should cool a 10-cell room from +20 °C to 0 °C over about 10 in-game hours.

10 in-game hours = 10 × 2500 ticks = 25000 ticks = 100 `TickRare` intervals.  
Over 100 intervals, 10 blocks melt ≈ at 1 block per interval at 20 °C.  

Working backwards: if each block absorbs enough to drop one 10-cell room by 2 °C per `TickRare`:
- Vanilla room heat capacity ≈ 4.0 energy/°C for a 10-cell room (approximate).
- Energy per block: `2.0 °C × 4.0 = 8.0 energy units`.

Suggested defaults:
```
ColdEnergyPerBlock  = 8.0f    // energy units absorbed per block melted
TempDropPerBlock    = 2.0f    // °C of room temp that triggers one extra block to melt
MeltThreshold       = 0.0f    // °C — below this, no melting
```

These are starting values to validate in-game. Adjust upward if ice feels too weak, downward if it trivially freezes rooms.

### 4.4 Zone Labels and Translation Keys

| Key | English string |
|---|---|
| `IceHarvest_IceHarvestZone` | Ice Harvest Zone |
| `IceHarvest_DesignatorIceHarvestZoneDesc` | Designate an area where pawns will harvest ice from frozen water. |
| `IceHarvest_CommandAllowHarvestIce` | Allow harvesting |
| `IceHarvest_CommandAllowHarvestIceDesc` | Enables or disables ice harvesting in this zone. |
| `IceHarvest_HarvestableCells` | Harvestable cells |

---

## 5. Mod File Structure

```
IceHarvestVanilla/
  About/
    About.xml                         ← mod metadata; no hard dependencies
  Assemblies/
    IceHarvestVanilla.dll
  Defs/
    JobDef_IceHarvest.xml             ← JobDef + WorkGiverDef (workType: Mining)
    ThingDef_IceBlock.xml             ← IceBlock ThingDef
  Languages/
    English/
      Keyed/
        IceHarvest.xml                ← translation keys (§4.4)
  Source/
    IceHarvestDefOf.cs
    Zone_IceHarvest.cs
    Designator_IceHarvest.cs
    WorkGiver_HarvestIce.cs
    JobDriver_HarvestIce.cs
    Comp_IceMelt.cs
    IceHarvestVanilla.csproj
```

No patches required — no delQ thermal properties to add, no vanilla ThingDef modifications.

---

## 6. Odyssey DLC Guard Pattern

All code paths that read `ThinIce` terrain or call `RemoveTempTerrain` must be guarded:

```csharp
// In Zone_IceHarvest.IsHarvestable
if (!ModsConfig.OdysseyActive) return false;

// In JobDriver_HarvestIce.FailOn
if (!ModsConfig.OdysseyActive) return true;   // cancel job gracefully

// In Comp_IceMelt.CompTickRare
if (!ModsConfig.OdysseyActive) return;
```

This ensures the mod can be included in a modlist without Odyssey and simply does nothing, rather than throwing errors.

---

## 7. What Is Not Needed

The following delQ components have **no equivalent** in this spinoff and should not be ported:

| delQ component | Why not needed |
|---|---|
| `TemperatureGrid` | Vanilla uses `GenTemperature`; no custom grid |
| `TuningDef` / `ThermalProps` | No per-cell simulation; constants are hardcoded |
| `MapExtensions` | No `ThermalGrid()` extension needed |
| `HeatSourceRegistryDef` | No heat source simulation |
| `TemperatureGrid.PhaseTransitions` | Vanilla `FreezeManager` handles freeze/thaw |
| `Setup.cs` / `Harmony patches` | No method patching required |
| `Settings.cs` | Optional; can add mod settings later if desired |
| `Patches_BuildingThermal.xml` | No thermal property injection |

---

## 8. Implementation Sequence

1. **Create project** — new `.csproj` targeting 1.6 assemblies; reference `Assembly-CSharp.dll`, `UnityEngine.CoreModule.dll`.
2. **Port `Zone_IceHarvest`** — swap thickness check for terrain check; add Odyssey guard.
3. **Port `WorkGiver_HarvestIce`** — update DefOf reference only.
4. **Port `JobDriver_HarvestIce`** — replace FailOn, baseTicks, and complete toil as described in §3.3.
5. **Write `Comp_IceMelt`** (new) — use `CompTickRare`, vanilla temp, `GenTemperature.PushHeat`.
6. **Port `Designator_IceHarvest`** — update labels/namespace only.
7. **Write `IceHarvestDefOf`** — new defNames.
8. **Write Defs XMLs** — `JobDef_IceHarvest.xml`, `ThingDef_IceBlock.xml` (tickerType `Rare`).
9. **Write `About.xml`** — list Odyssey as a soft requirement, not a hard dependency.
10. **In-game tuning** — validate `ColdEnergyPerBlock` and `TempDropPerBlock` values (§4.3); adjust yield range (§4.1).
