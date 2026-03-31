# Vanilla Ice — Milestones

## M1 — Core Implementation ⬜
- [x] Project scaffolding (csproj, sln, About.xml, .gitignore)
- [x] `Zone_IceHarvest` — ThinIce terrain check, Odyssey guard, allow/disallow gizmo
- [x] `Designator_ZoneAdd_IceHarvest` — zone placement designator
- [x] `WorkGiver_HarvestIce` — nearest-zone job assignment
- [x] `JobDriver_HarvestIce` — walk → carve → `RemoveTempTerrain` + spawn blocks
- [x] `Comp_IceMelt` — `CompTickRare` melt via `GenTemperature.PushHeat`
- [x] `ThingDef_IceBlock.xml` — `tickerType=Rare`, correct comp reference
- [x] `JobDef_IceHarvest.xml` + `WorkGiverDef`
- [x] Translation strings (English)
- [ ] **In-game test**: verify zone designation, pawn harvesting, block spawning, terrain revert

## M2 — Tuning & Polish ⬜
- [ ] Validate `ColdEnergyPerBlock` / `TempDropPerBlock` in-game (see DESIGN.md §Tuning)
- [ ] Validate yield range (2–5 blocks) feels appropriate
- [ ] Validate refreeze timing is reasonable in Arctic biome
- [ ] Custom zone texture (ice-blue, distinct from stockpile)
- [ ] Custom designator icon (`UI/Designators/ZoneCreate_IceHarvest`)

## M3 — Publishing ⬜
- [ ] Steam Workshop preview image
- [ ] Final About.xml description
- [ ] README polish
- [ ] Upload to Steam Workshop (CheaterEater-RM account)
