# Ice Harvesting & Ice Fishing — Build Guide

A developer guide for implementing two zone-based features on top of RimWorld 1.6:

1. **Ice Harvesting** — pawns carve ice blocks from cells within a designated zone.
2. **Ice Fishing** — pawns carve a hole through thin ice in a designated zone, then fish.

The guide is organized by layer: **Zone**, **Designator**, **WorkGiver**, and **JobDriver**, following the same layered architecture RimWorld uses for Growing and Fishing zones.

This guide was built for delQ and may not be applicable to vanilla Rimworld in places.

---

## 0. Architecture Overview

Each feature follows this four-layer stack:

```
Zone_IceHarvest / Zone_IceFishing
    ↑ created by
Designator_ZoneAdd_IceHarvest / Designator_ZoneAdd_IceFishing
    ↑ scanned by
WorkGiver_HarvestIce / WorkGiver_IceFish (NonScanJob pattern)
    ↑ executes via
JobDriver_HarvestIce / JobDriver_IceFish
```

XML wires the WorkGivers and WorkTypes into the game; C# implements all behavior.

The IceBlocks mod (HoeenCoder) is the best reference for the **harvested product** side — it defines ice blocks as a `ThingDef` that can be placed and that melts based on temperature. We are concerned with the **harvesting** side that IceBlocks leaves implicit, plus the new ice-fishing variant.

---

## 1. The Zone Classes

### 1a. `Zone_IceHarvest`

Inherits from `Verse.Zone` (not `Zone_Growing`, because there's no plant management needed). The key decisions it needs to store:

- Whether harvesting is `allowed` (mirrors the fishing/growing toggle).
- Whether to use a minimum-temperature guard: cells should only be harvestable when they are genuinely frozen. Delegate this check to a helper that queries the terrain.

**What makes a cell harvestable?** A cell where:
- The terrain is ice (check `TerrainDef` category, or the specific `TerrainDef` tags set by your mod — for vanilla integration check `terrain.IsWater == false` combined with your delQ-registered ice terrain, or simply check the `TerrainDef.defName` against known ice defs).
- The cell is not already dug out (no open water in the same cell).
- The cell is not occupied by a structure (`!c.GetFirstBuilding(map)` is null or impassable).

```csharp
namespace IceWork;

public class Zone_IceHarvest : Zone
{
    private bool allowed = true;

    // true = only harvest cells that are currently frozen terrain
    public bool Allowed => allowed;

    protected override Color NextZoneColor => new Color(0.5f, 0.85f, 1f);   // light ice blue

    public Zone_IceHarvest() { }

    public Zone_IceHarvest(ZoneManager zoneManager)
        : base("IceHarvestZone", zoneManager) { }

    public bool IsHarvestable(IntVec3 c)
    {
        // Replace this predicate with whatever terrain check your mod uses.
        // For vanilla: IceSheet terrain, ShallowWater frozen by delQ, etc.
        TerrainDef t = c.GetTerrain(Map);
        return t != null && IsIceTerrain(t);
    }

    // Override to only enumerate cells that still have ice.
    public IEnumerable<IntVec3> HarvestableCells =>
        cells.Where(IsHarvestable);

    private static bool IsIceTerrain(TerrainDef t)
    {
        // Expand this list to match your TerrainDefs.
        return t.defName is "Ice" or "PackedIce" or "ThinIce";
        // Or use: return t.HasTag("IceTerrain");  if you add a tag in XML.
    }

    public override IEnumerable<Gizmo> GetZoneAddGizmos()
    {
        yield return DesignatorUtility.FindAllowedDesignator<Designator_ZoneAdd_IceHarvest_Expand>();
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        // Allow-toggle, matching the fishing zone pattern.
        foreach (Gizmo g in base.GetGizmos()) yield return g;
        yield return new Command_Toggle
        {
            defaultLabel = "CommandAllowHarvestIce".Translate(),
            defaultDesc  = "CommandAllowHarvestIceDesc".Translate(),
            icon         = TexCommand.ForbidOff,
            hotKey       = KeyBindingDefOf.Command_ItemForbid,
            isActive     = () => allowed,
            toggleAction = () => { allowed = !allowed; }
        };
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref allowed, "allowed", defaultValue: true);
    }

    public override string GetInspectString()
    {
        int count = HarvestableCells.Count();
        return base.GetInspectString() +
               $"\n{"HarvestableCells".Translate()}: {count}";
    }
}
```

**Key `Zone` base class contract** (from `Verse/Zone.cs`):
- `ExposeData()` — must call `base.ExposeData()` first; handles `cells`, `ID`, `label`, `color`.
- `AddCell(IntVec3)` / `RemoveCell(IntVec3)` — base handles grid registration; override only if you need extra validation.
- `GetZoneAddGizmos()` — return the expand designator so the toolbar "expand" button appears.
- `NextZoneColor` — abstract, must return a unique color family.

---

### 1b. `Zone_IceFishing`

This zone is slightly richer because it has **two sub-tasks**: (1) carve a hole if none exists, (2) fish through the hole. It can reuse `Zone_Fishing` as inspiration but must work over ice rather than open water.

```csharp
public class Zone_IceFishing : Zone
{
    private bool allowed = true;

    // Cells where a hole has already been carved (terrain changed to ThinIce or OpenWater).
    // Persisted so pawns don't re-carve on reload.
    public HashSet<IntVec3> CarvedCells = new HashSet<IntVec3>();

    public bool Allowed => allowed;
    protected override Color NextZoneColor => new Color(0.3f, 0.7f, 1f);

    public Zone_IceFishing() { }
    public Zone_IceFishing(ZoneManager zoneManager)
        : base("IceFishingZone", zoneManager) { }

    /// Cells that are solid ice and need to be carved first.
    public IEnumerable<IntVec3> SolidIceCells =>
        cells.Where(c => IsSolidIce(c) && !CarvedCells.Contains(c));

    /// Cells where a hole exists and fishing can happen.
    public IEnumerable<IntVec3> OpenHoleCells =>
        cells.Where(c => IsOpenHole(c));

    public bool NeedsCarving => SolidIceCells.Any();
    public bool CanFish     => OpenHoleCells.Any();

    private bool IsSolidIce(IntVec3 c)
    {
        TerrainDef t = c.GetTerrain(Map);
        return t != null && (t.defName == "Ice" || t.defName == "PackedIce");
    }

    private bool IsOpenHole(IntVec3 c)
    {
        // After carving, terrain becomes ThinIce or your custom HoleInIce terrain.
        TerrainDef t = c.GetTerrain(Map);
        return t != null && (t.defName == "ThinIce" || t.defName == "HoleInIce");
    }

    public void Notify_Carved(IntVec3 c) => CarvedCells.Add(c);

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (Gizmo g in base.GetGizmos()) yield return g;
        yield return new Command_Toggle
        {
            defaultLabel = "CommandAllowIceFish".Translate(),
            icon         = TexCommand.ForbidOff,
            isActive     = () => allowed,
            toggleAction = () => { allowed = !allowed; }
        };
    }

    public override IEnumerable<Gizmo> GetZoneAddGizmos()
    {
        yield return DesignatorUtility.FindAllowedDesignator<Designator_ZoneAdd_IceFishing_Expand>();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref allowed, "allowed", defaultValue: true);
        Scribe_Collections.Look(ref CarvedCells, "carvedCells", LookMode.Value);
        if (CarvedCells == null) CarvedCells = new HashSet<IntVec3>();
    }
}
```

> **Terrain change mechanic:** When a pawn completes the carving toil, call `map.terrainGrid.SetTerrain(cell, TerrainDefOf.ThinIce)` (or your custom HoleInIce def). This is a permanent map change, so it also integrates with delQ's terrain tracking automatically. The `CarvedCells` set is a belt-and-suspenders tracker in case terrain detection is ambiguous between different ice states.

---

## 2. The Designators

Designators handle drawing the zone on the map and constructing the Zone object. They inherit from `Designator_ZoneAdd` (which handles multi-cell drag logic, expanding existing zones, and registering new ones with `ZoneManager`).

The minimum contract for `Designator_ZoneAdd`:
- Set `zoneTypeToPlace` in the constructor.
- Override `MakeNewZone()` to return a new instance.
- Override `CanDesignateCell()` if you need to restrict placement (e.g., only on ice terrain).

### 2a. Ice Harvest Designator

```csharp
public class Designator_ZoneAdd_IceHarvest : Designator_ZoneAdd
{
    protected override string NewZoneLabel => "IceHarvestZone".Translate();

    public Designator_ZoneAdd_IceHarvest()
    {
        zoneTypeToPlace = typeof(Zone_IceHarvest);
        defaultLabel    = "IceHarvestZone".Translate();
        defaultDesc     = "DesignatorIceHarvestZoneDesc".Translate();
        icon            = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_IceHarvest");
        tutorTag        = "ZoneAdd_IceHarvest";
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 c)
    {
        if (!base.CanDesignateCell(c).Accepted) return false;
        // Optional: only allow placement on ice terrain.
        // TerrainDef t = c.GetTerrain(base.Map);
        // if (!Zone_IceHarvest.IsIceTerrain(t)) return "NotIceTerrain".Translate();
        // Allowing placement on any cell keeps it flexible (like growing zones on dirt).
        return true;
    }

    protected override Zone MakeNewZone()
        => new Zone_IceHarvest(Find.CurrentMap.zoneManager);
}

// The "Expand" variant — required by the zone's GetZoneAddGizmos():
public class Designator_ZoneAdd_IceHarvest_Expand : Designator_ZoneAdd_IceHarvest
{
    protected override bool ShowRightClickHideOptions => false;

    public Designator_ZoneAdd_IceHarvest_Expand()
    {
        defaultLabel = "DesignatorExpandExistingZone".Translate();
        defaultDesc  = "DesignatorExpandExistingZoneDesc".Translate();
        icon         = TexButton.Add;
    }
}
```

### 2b. Ice Fishing Designator

```csharp
public class Designator_ZoneAdd_IceFishing : Designator_ZoneAdd
{
    protected override string NewZoneLabel => "IceFishingZone".Translate();

    public Designator_ZoneAdd_IceFishing()
    {
        zoneTypeToPlace = typeof(Zone_IceFishing);
        defaultLabel    = "IceFishingZone".Translate();
        defaultDesc     = "DesignatorIceFishingZoneDesc".Translate();
        icon            = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_IceFishing");
        tutorTag        = "ZoneAdd_IceFishing";
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 c)
    {
        if (!base.CanDesignateCell(c).Accepted) return false;
        // Must be over ice terrain (solid or thin).
        TerrainDef t = c.GetTerrain(base.Map);
        if (t == null || !(t.defName is "Ice" or "PackedIce" or "ThinIce"))
            return "MustBeOnIce".Translate();
        return true;
    }

    protected override Zone MakeNewZone()
        => new Zone_IceFishing(Find.CurrentMap.zoneManager);
}

public class Designator_ZoneAdd_IceFishing_Expand : Designator_ZoneAdd_IceFishing
{
    protected override bool ShowRightClickHideOptions => false;
    public Designator_ZoneAdd_IceFishing_Expand()
    {
        defaultLabel = "DesignatorExpandExistingZone".Translate();
        icon         = TexButton.Add;
    }
}
```

**Wiring Designators into the UI** — add them to a `DesignationCategoryDef` in XML:

```xml
<DesignationCategoryDef>
  <defName>IceWork</defName>
  <label>Ice Work</label>
  <order>45</order>
  <specialDesignatorClasses>
    <li>IceWork.Designator_ZoneAdd_IceHarvest</li>
    <li>IceWork.Designator_ZoneAdd_IceFishing</li>
  </specialDesignatorClasses>
</DesignationCategoryDef>
```

Alternatively, add them to the existing Zones category: look up `DesignationCategoryDef` `Zone` in the base game and PatchOperation-add your entries.

---

## 3. The WorkGivers

WorkGivers are the bridge between the pawn's AI work scheduler and the JobDriver. The clearest RimWorld pattern for zone-based work is the `NonScanJob` pattern used by `WorkGiver_Fish`:

- `NonScanJob` is called once per work tick per pawn, not per-cell. It finds the best zone, picks a target cell, and returns a constructed `Job`.
- This is more efficient than `GetThingPriority`/`HasJobOnThing` (the scanner pattern) when the target is a terrain cell rather than a `Thing`.

### 3a. `WorkGiver_HarvestIce`

```csharp
public class WorkGiver_HarvestIce : WorkGiver_Scanner
{
    public override ThingRequest PotentialWorkThingRequest
        => ThingRequest.ForUndefined();    // We use NonScanJob, not per-Thing scanning

    public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
        => Enumerable.Empty<IntVec3>();    // Suppress cell scanning entirely

    public override Job NonScanJob(Pawn pawn)
    {
        // Find the nearest allowed ice harvest zone with harvestable cells.
        Zone_IceHarvest bestZone = null;
        float bestDistSq = float.MaxValue;

        foreach (Zone z in pawn.Map.zoneManager.AllZones)
        {
            if (z is not Zone_IceHarvest hz) continue;
            if (!hz.Allowed) continue;
            if (!hz.HarvestableCells.Any()) continue;

            float distSq = pawn.Position.DistanceToSquared(hz.Cells[0]);
            if (distSq < bestDistSq)
            {
                bestZone  = hz;
                bestDistSq = distSq;
            }
        }

        if (bestZone == null) return null;

        // Pick a reachable harvestable cell.
        foreach (IntVec3 c in bestZone.HarvestableCells.InRandomOrder())
        {
            if (!pawn.CanReserveAndReach(c, PathEndMode.Touch, Danger.Some))
                continue;

            // Build the job: TargetA = cell to harvest.
            return JobMaker.MakeJob(IceWorkJobDefOf.HarvestIce, c);
        }

        return null;
    }
}
```

### 3b. `WorkGiver_IceFish`

The ice fishing WorkGiver has two phases it must prioritize:

1. If any cell needs carving, issue a carving job first.
2. If holes exist and fish are available, issue a fishing job.

```csharp
public class WorkGiver_IceFish : WorkGiver_Scanner
{
    public override Job NonScanJob(Pawn pawn)
    {
        Zone_IceFishing bestZone = null;
        float bestDistSq = float.MaxValue;

        foreach (Zone z in pawn.Map.zoneManager.AllZones)
        {
            if (z is not Zone_IceFishing fz) continue;
            if (!fz.Allowed) continue;
            // Zone is useful if it needs carving OR can be fished.
            if (!fz.NeedsCarving && !fz.CanFish) continue;

            float distSq = pawn.Position.DistanceToSquared(fz.Cells[0]);
            if (distSq < bestDistSq)
            {
                bestZone  = fz;
                bestDistSq = distSq;
            }
        }

        if (bestZone == null) return null;

        // Phase 1: Carving takes priority over fishing.
        if (bestZone.NeedsCarving)
        {
            foreach (IntVec3 c in bestZone.SolidIceCells.InRandomOrder())
            {
                if (!pawn.CanReserveAndReach(c, PathEndMode.Touch, Danger.Some))
                    continue;
                return JobMaker.MakeJob(IceWorkJobDefOf.CarveIceHole, c);
            }
        }

        // Phase 2: Fish through an open hole.
        if (bestZone.CanFish)
        {
            foreach (IntVec3 c in bestZone.OpenHoleCells.InRandomOrder())
            {
                if (!pawn.CanReserveAndReach(c, PathEndMode.Touch, Danger.Some))
                    continue;
                // Reuse vanilla Fish job if the cell now has open water type.
                // Or use a custom job if you need different behavior.
                IntVec3 standSpot = WorkGiver_Fish.BestStandSpotFor(pawn, c);
                if (!standSpot.IsValid) continue;
                return JobMaker.MakeJob(JobDefOf.Fish, c, standSpot);
            }
        }

        return null;
    }
}
```

**Registering WorkGivers in XML** — add to a `WorkTypeDef`:

```xml
<WorkTypeDef>
  <defName>IceWork</defName>
  <label>ice work</label>
  <pawnLabel>ice worker</pawnLabel>
  <gerundLabel>ice working</gerundLabel>
  <description>Harvesting ice blocks and ice fishing.</description>
  <workGiversByPriority>
    <li>
      <defName>HarvestIce</defName>
      <giverClass>IceWork.WorkGiver_HarvestIce</giverClass>
      <workType>IceWork</workType>
      <priorityInType>80</priorityInType>
    </li>
    <li>
      <defName>IceFish</defName>
      <giverClass>IceWork.WorkGiver_IceFish</giverClass>
      <workType>IceWork</workType>
      <priorityInType>70</priorityInType>
    </li>
  </workGiversByPriority>
</WorkTypeDef>
```

Or — if you don't want a new work type column — add the WorkGiverDefs to an existing type like `Mining` (lower priority than real mining) by patching the `<workGiversByPriority>` list.

---

## 4. The JobDrivers

### 4a. `JobDriver_HarvestIce`

Modeled on `JobDriver_Mine` (see `RimWorld/JobDriver_Mine.cs`). Key differences:
- Target is a terrain cell, not a `Thing`, so we don't call `TakeDamage` on a building.
- Completion replaces terrain and spawns ice block items.
- Skill gain uses `Mining` (ice carving is physical extraction).

```csharp
public class JobDriver_HarvestIce : JobDriver
{
    private const TargetIndex HarvestCellInd = TargetIndex.A;

    // Duration tuning: roughly 2000 ticks at mining speed 1.0 ≈ 33 seconds.
    private const float BaseHarvestTicks = 2000f;

    private IntVec3 HarvestCell => job.GetTarget(HarvestCellInd).Cell;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
        => pawn.Reserve(HarvestCell, job, 1, -1, ReservationLayerDefOf.Floor, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        // Fail if zone disappears or cell is no longer ice.
        this.FailOn(() => HarvestCell.GetZone(pawn.Map) is not Zone_IceHarvest hz || !hz.Allowed);
        this.FailOn(() => !IsIceTerrain(HarvestCell.GetTerrain(pawn.Map)));

        // Step 1: Walk to an adjacent cell (touch = stand next to the target).
        yield return Toils_Goto.GotoCell(HarvestCellInd, PathEndMode.Touch);

        // Step 2: Chip away at the ice.
        int ticks = Mathf.RoundToInt(BaseHarvestTicks / pawn.GetStatValue(StatDefOf.MiningSpeed));
        Toil carve = ToilMaker.MakeToil("CarveIce");
        carve.defaultCompleteMode = ToilCompleteMode.Delay;
        carve.defaultDuration     = ticks;
        carve.WithProgressBarToilDelay(HarvestCellInd);
        carve.tickAction = () =>
        {
            pawn.skills?.Learn(SkillDefOf.Mining, 0.07f);
            // Optional: particle effect each tick
        };
        carve.AddFinishAction(() =>
        {
            // Don't call this in tickAction — only on natural completion.
        });
        yield return carve;

        // Step 3: Completion — change terrain, spawn items.
        Toil complete = ToilMaker.MakeToil("CompleteIceHarvest");
        complete.initAction = () =>
        {
            IntVec3 c   = HarvestCell;
            Map     map = pawn.Map;

            // Change terrain: solid ice → open terrain (shallow water, mud, etc.)
            // Choose the "revealed" terrain appropriate to what was under the ice.
            map.terrainGrid.SetTerrain(c, TerrainDefOf.Ice); // or Remove and revert

            // Spawn ice blocks.
            Thing iceBlocks = ThingMaker.MakeThing(IceWorkThingDefOf.IceBlock);
            iceBlocks.stackCount = Rand.RangeInclusive(8, 20);
            GenPlace.TryPlaceThing(iceBlocks, c, map, ThingPlaceMode.Near);

            pawn.records.Increment(RecordDefOf.CellsMined);
        };
        yield return complete;
    }

    private static bool IsIceTerrain(TerrainDef t)
        => t != null && t.defName is "Ice" or "PackedIce";
}
```

> **Terrain revert:** Decide what the cell becomes after harvesting. The simplest model: the cell becomes bare ground or shallow water (if over water). This is a permanent change, which integrates naturally with delQ — the cell loses its ice insulation, and delQ's simulation will govern whether it re-freezes. You can optionally check `GenTemperature.TryGetTemperatureForCell` to decide which terrain to place.

---

### 4b. `JobDriver_CarveIceHole` (for ice fishing zone, carving phase)

```csharp
public class JobDriver_CarveIceHole : JobDriver
{
    private const TargetIndex CellInd = TargetIndex.A;
    private const float BaseCarveTimeTicks = 1500f;

    private IntVec3 TargetCell => job.GetTarget(CellInd).Cell;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
        => pawn.Reserve(TargetCell, job, 1, -1, ReservationLayerDefOf.Floor, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => TargetCell.GetZone(pawn.Map) is not Zone_IceFishing fz || !fz.Allowed);

        yield return Toils_Goto.GotoCell(CellInd, PathEndMode.Touch);

        int ticks = Mathf.RoundToInt(BaseCarveTimeTicks / pawn.GetStatValue(StatDefOf.MiningSpeed));
        Toil carve = ToilMaker.MakeToil("CarveHole");
        carve.defaultCompleteMode = ToilCompleteMode.Delay;
        carve.defaultDuration     = ticks;
        carve.WithProgressBarToilDelay(CellInd);
        carve.tickAction = () => pawn.skills?.Learn(SkillDefOf.Mining, 0.05f);
        yield return carve;

        Toil complete = ToilMaker.MakeToil("CompleteCarveHole");
        complete.initAction = () =>
        {
            IntVec3 c   = TargetCell;
            Map     map = pawn.Map;

            // Set terrain to ThinIce (or a custom HoleInIce terrain).
            // ThinIce is a vanilla terrain that fishing zones already recognize.
            map.terrainGrid.SetTerrain(c, TerrainDefOf.ThinIce);

            // Notify the zone a hole was carved.
            if (c.GetZone(map) is Zone_IceFishing fz)
                fz.Notify_Carved(c);
        };
        yield return complete;
    }
}
```

After this job completes, the WorkGiver's next scan will see the cell as an `OpenHoleCell` and issue a `JobDef.Fish` job using vanilla `JobDriver_Fish` — no additional custom code needed for the fishing phase, because `ThinIce` is exactly what vanilla checks for in `Zone_Fishing.AllFishableCellsFrozenNow`.

---

## 5. JobDefs (XML)

```xml
<JobDef>
  <defName>HarvestIce</defName>
  <driverClass>IceWork.JobDriver_HarvestIce</driverClass>
  <reportString>harvesting ice.</reportString>
  <collideWithPawns>false</collideWithPawns>
</JobDef>

<JobDef>
  <defName>CarveIceHole</defName>
  <driverClass>IceWork.JobDriver_CarveIceHole</driverClass>
  <reportString>carving an ice fishing hole.</reportString>
  <collideWithPawns>false</collideWithPawns>
</JobDef>
```

Reference them in C# via a static class (generated from static field lookups or a `[DefOf]`):

```csharp
[DefOf]
public static class IceWorkJobDefOf
{
    public static JobDef HarvestIce;
    public static JobDef CarveIceHole;
}
```

---

## 6. The Ice Block ThingDef

The IceBlocks mod defines this well — the key fields for an ice block `ThingDef`:

```xml
<ThingDef ParentName="ResourceBase">
  <defName>IceBlock</defName>
  <label>ice block</label>
  <graphicData>
    <texPath>Things/Item/Resource/IceBlock</texPath>
    <graphicClass>Graphic_StackCount</graphicClass>
    <color>(180,235,255)</color>
  </graphicData>
  <statBases>
    <MaxHitPoints>40</MaxHitPoints>
    <Mass>3</Mass>
    <Flammability>0</Flammability>
  </statBases>
  <stackLimit>75</stackLimit>
  <thingCategories>
    <li>StoneChunks</li>   <!-- or a custom category -->
  </thingCategories>
  <!-- Melting: implement via ThingComp that checks cell temperature each N ticks -->
  <comps>
    <li Class="IceWork.CompProperties_Melt">
      <meltAboveTemperature>-2</meltAboveTemperature>   <!-- Celsius -->
      <ticksBetweenChecks>500</ticksBetweenChecks>
    </li>
  </comps>
</ThingDef>
```

For the melt comp, you'd check `GenTemperature.TryGetTemperatureForCell(parent.Position, parent.Map, out float temp)` and call `parent.Destroy()` if the temperature exceeds the threshold. This is independent of delQ but integrates cleanly with it.

---

## 7. Full Data Flow Summary

### Ice Harvesting
```
Player draws zone → Designator_ZoneAdd_IceHarvest.DesignateMultiCell()
  → Zone_IceHarvest created, cells registered in ZoneManager grid

Work scheduler → WorkGiver_HarvestIce.NonScanJob()
  → iterates zoneManager.AllZones, finds Zone_IceHarvest with HarvestableCells
  → returns JobMaker.MakeJob(HarvestIce, targetCell)

JobDriver_HarvestIce executes:
  1. GotoCell (PathEndMode.Touch)
  2. Delay toil (ticks = BaseHarvestTicks / MiningSpeed) with progress bar
  3. Complete toil:
       → map.terrainGrid.SetTerrain(cell, revealedTerrain)
       → GenPlace.TryPlaceThing(IceBlock, ...)
```

### Ice Fishing
```
Player draws zone → Designator_ZoneAdd_IceFishing.DesignateMultiCell()
  → Zone_IceFishing created

Work scheduler → WorkGiver_IceFish.NonScanJob()
  → finds Zone_IceFishing with SolidIceCells → issues CarveIceHole job
  → (or) finds Zone_IceFishing with OpenHoleCells → issues vanilla Fish job

JobDriver_CarveIceHole executes:
  1. GotoCell
  2. Delay toil (carving animation/progress)
  3. Complete toil:
       → map.terrainGrid.SetTerrain(cell, TerrainDefOf.ThinIce)
       → zone.Notify_Carved(cell)

On next work scan: WorkGiver_IceFish sees OpenHoleCells → issues JobDef.Fish
JobDriver_Fish (vanilla) executes fishing as normal
```

---

## 8. delQ Integration Notes

The integration with delQ is intentionally minimal on the game logic side:

- **Terrain change events**: When `map.terrainGrid.SetTerrain()` is called (both carving and harvesting), delQ should detect this via its existing terrain change tracking and update thermal properties for that cell. No special notification is needed from these drivers.
- **Ice availability**: Your `IsIceTerrain()` predicate is naturally driven by whatever terrain delQ places when water freezes. As long as delQ uses named TerrainDefs for ice states, the zone's harvestability check just needs to enumerate those defNames.
- **Re-freezing harvested cells**: After a cell is harvested (terrain reverted to open water), delQ's simulation will govern whether it re-freezes. This creates the natural emergent gameplay loop: cells re-freeze in cold weather and can be harvested again.
- **Temperature guard (optional)**: You can add a `zone.Allowed` condition check in the WorkGiver that reads the cell temperature via `GenTemperature.TryGetTemperatureForCell` and refuses to issue jobs if the temperature is above 0°C (indicating the ice might be melting). This is cosmetic polish but keeps the feature coherent with delQ's temperature world.

---

## 9. Checklist Before Testing

- [ ] `Zone_IceHarvest` and `Zone_IceFishing` serialize correctly (`ExposeData` round-trip)
- [ ] Zones appear in the zone list after placement
- [ ] Zone color is visible on the map overlay
- [ ] Expand designator works (zone grows when you drag adjacent cells)
- [ ] Zone deletes cleanly when all cells are removed
- [ ] WorkGiver produces jobs on ice terrain cells
- [ ] WorkGiver produces no jobs when `Allowed = false`
- [ ] `TryMakePreToilReservations` prevents two pawns targeting the same cell
- [ ] `FailOn` conditions abort the job if the zone is deleted mid-job
- [ ] Ice blocks appear in stockpile after harvest
- [ ] Carve toil sets terrain to `ThinIce`; fishing job issues immediately after
- [ ] `Zone_IceFishing.CarvedCells` persists correctly after save/load
- [ ] delQ updates thermal properties after terrain changes (verify in delQ debug overlay)
