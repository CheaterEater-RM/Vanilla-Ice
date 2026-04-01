# RimWorld Snow Clearing Mechanics
*RimWorld 1.6 · For Mod Developers*

---

## Overview

Snow in RimWorld is **not** tracked as individual items, but as a **continuous depth value** stored in `SnowGrid`. Snow clearing is a **WorkGiver-driven job**, not a zone or designation system. This guide explains the mechanics for a mod that wants to convert cleared snow into collectible items.

---

## Snow Storage: SnowGrid

### The Data Structure

Snow is stored per-cell as a **depth value** (float) in `Verse.SnowGrid`:

```csharp
// Verse/SnowGrid.cs
public class SnowGrid
{
    private float[] grid;  // One float per map cell
    private Map map;
    
    public float GetSnowDepth(IntVec3 cell)       // Read depth at a cell
    public void SetSnowDepth(IntVec3 cell, float depth)
    public void AddSnow(IntVec3 cell, float amount)
    public void RemoveSnow(IntVec3 cell, float amount)
    public void Tick()  // Snow accumulates each tick when weather permits
}
```

**Key properties:**
- **Max depth:** `1.0f` (full cell of snow)
- **Visual rendering:** `SectionLayer_Snow` renders depth as grayscale overlay
- **Accumulation:** `SnowGrid.Tick()` adds snow each tick during snowfall weather
- **No Def-level control:** Snow depth is a raw float; there's no `SnowDef` or individual snow pieces

---

## Snow Clearing: The Job System

### WorkGiver → JobDriver Pipeline

Snow clearing is **not** an automatic zone. Instead, vanilla uses a WorkGiver to create jobs on-demand:

```
WorkGiver_ClearSnowOrSand (scans map for cells with snow)
    ↓
    → Creates JobDef_ClearSnowOrSand for eligible pawns
    ↓
    → JobDriver_ClearSnowAndSand executes the job
    ↓
    → SnowGrid.RemoveSnow() reduces depth
```

### WorkGiver_ClearSnowOrSand

Located in `RimWorld\WorkGiver_ClearSnowOrSand.cs`

```csharp
public class WorkGiver_ClearSnowOrSand : WorkGiver_Scanner
{
    public override PathEndMode PathEndMode => PathEndMode.OnCell;
    
    public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
    {
        // Checks if cell has snow/sand above threshold
        // Returns true if snow depth > threshold OR forced designation
        // NOTE: This does NOT check a designation or zone!
    }
    
    public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
    {
        // Returns JobDef_ClearSnowOrSand for this cell
    }
}
```

**Critical insight:** There is **no DesignationDef** for snow clearing. The WorkGiver scans based on **snow depth alone**. Snow clears automatically when depth exceeds a threshold—*unless* the player has disabled snow clearing in the Zone system (see below).

### JobDriver_ClearSnowAndSand

Located in `RimWorld\JobDriver_ClearSnowAndSand.cs`

```csharp
public class JobDriver_ClearSnowAndSand : JobDriver
{
    public override IEnumerable<Toil> MakeNewToils()
    {
        // 1. Travel to target cell
        // 2. Perform animation/work
        // 3. Call SnowGrid.RemoveSnow(cell, removeAmount)
        // 4. Repeat until snow depth below threshold
    }
}
```

**What actually clears the snow:**
```csharp
// Inside JobDriver_ClearSnowAndSand
map.snowGrid.RemoveSnow(targetCell, amount);  // Typically ~0.1 to 0.3 per job
```

This is a **direct modification** to the SnowGrid float array. There is **no item created**.

---

## Zone-Based Control: Snow Clearing Zones

While snow itself is not a zone, RimWorld has **zone-level settings** that control whether snow clears:

```csharp
// RimWorld/Zone.cs
public class Zone
{
    public bool allowSnowClearing = true;  // Players can toggle per-zone
}
```

When a pawn tries to clear snow in a zone with `allowSnowClearing = false`, the WorkGiver rejects the cell. This is the *only* player-accessible control for snow clearing (besides weather).

---

## Patching Opportunities for Snow-as-Item

To convert cleared snow into collectible items, you have two main hooks:

### Option 1: Postfix SnowGrid.RemoveSnow (Recommended)

**Hook:** `SnowGrid.RemoveSnow(IntVec3 cell, float amount)`

```csharp
[HarmonyPatch(typeof(SnowGrid), nameof(SnowGrid.RemoveSnow))]
public static class SnowGrid_RemoveSnow_Patch
{
    static void Postfix(IntVec3 cell, float amount, Map ___map)
    {
        // Convert removed snow to item
        float snowVolume = amount;  // amount is depth removed
        int itemCount = Mathf.CeilToInt(snowVolume * ItemsPerDepthUnit);
        
        Thing snowItem = ThingMaker.MakeThing(YourSnowItemDef, null);
        snowItem.stackCount = itemCount;
        GenPlace.TryPlaceThing(snowItem, cell, ___map, ThingPlaceMode.Near);
    }
}
```

**Pros:**
- Catches *all* snow removal (manual clearing, melting, etc.)
- Simple and low-overhead

**Cons:**
- Fires even for vanilla automatic melting
- Need to extract the `Map` via triple-underscore parameter injection

### Option 2: Postfix JobDriver_ClearSnowAndSand.MakeNewToils

**Hook:** Modify toils to create item before calling `RemoveSnow`

```csharp
[HarmonyPatch(typeof(JobDriver_ClearSnowAndSand), nameof(JobDriver_ClearSnowAndSand.MakeNewToils))]
public static class JobDriver_ClearSnowAndSand_MakeNewToils_Patch
{
    static void Postfix(ref IEnumerable<Toil> __result, JobDriver_ClearSnowAndSand __instance)
    {
        // Wrap __result to create items *before* vanilla RemoveSnow is called
        // This ensures only player-cleared snow becomes items
    }
}
```

**Pros:**
- Only affects manual clearing (not weather/melting)
- Clear causality: player action → item

**Cons:**
- More complex (toil wrapping)
- Transpiler may be required for fine control

---

## WorkGiver Threshold & Depth

### When Does Snow Clear?

The WorkGiver scans for snow based on a **threshold** (typically `0.1f` or higher):

```csharp
// Pseudocode from WorkGiver_ClearSnowOrSand
float depth = map.snowGrid.GetSnowDepth(cell);
if (depth > 0.1f)  // Threshold varies; check source for exact value
{
    return true;  // Pawn can take clearing job
}
```

**Player control:**
- If a zone has `allowSnowClearing = true`, pawns will clear down to near zero
- If `allowSnowClearing = false`, no jobs are created for that zone

---

## Save/Load Considerations

If you're tracking cleared snow in a custom MapComponent:

```csharp
public class SnowMod_MapComponent : MapComponent
{
    public int totalSnowCleared = 0;
    
    public override void ExposeData()
    {
        Scribe_Values.Look(ref totalSnowCleared, "totalSnowCleared");
    }
}
```

When the player removes your mod mid-save, the MapComponent is simply discarded—no risk of data corruption.

---

## Performance Notes

- `SnowGrid.RemoveSnow()` is O(1) — just updating a float array
- `WorkGiver_ClearSnowOrSand.HasJobOnThing()` runs per pawn per scan — keep it fast
- Creating items per-removal can add GC pressure — batch or use object pooling if snow clears frequently

---

## Example: Simple Snow Item Mod

```csharp
// 1. Define your snow item (XML)
// <ThingDef>
//   <defName>ClearedSnow</defName>
//   <label>cleared snow</label>
//   ...
// </ThingDef>

// 2. Patch SnowGrid.RemoveSnow to create items
[HarmonyPatch(typeof(SnowGrid), nameof(SnowGrid.RemoveSnow))]
public static class SnowGrid_RemoveSnow_Patch
{
    static void Postfix(IntVec3 cell, float amount, Map ___map)
    {
        const float DEPTH_PER_ITEM = 0.1f;  // Tune based on your design
        int itemCount = Mathf.CeilToInt(amount / DEPTH_PER_ITEM);
        
        if (itemCount > 0)
        {
            Thing item = ThingMaker.MakeThing(ThingDefOf_YourMod.ClearedSnow);
            item.stackCount = itemCount;
            GenPlace.TryPlaceThing(item, cell, ___map, ThingPlaceMode.Near);
        }
    }
}
```

---

## Key Takeaways for Mod Design

1. **Snow is not an item by default** — it's a continuous depth float per cell
2. **No DesignationDef exists** — snow clearing is automatic based on WorkGiver scanning
3. **You must patch** `SnowGrid.RemoveSnow()` or `JobDriver_ClearSnowAndSand` to track/convert snow
4. **Zone control is the only player lever** — `Zone.allowSnowClearing` on/off
5. **Performance matters** — snow can accumulate rapidly; item creation per-removal can be costly
6. **Consider save compatibility** — new items in old saves will spawn on load; track totals separately if needed

