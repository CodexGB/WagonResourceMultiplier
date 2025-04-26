# Changelog

## [1.0.3] - 2024-04-26
### Added
- Initial public release of WagonResourceMultiplier.

### Fixed
- Prevented server startup crashes caused by RustEdit map placeholder entities.
- Skips fake/ghost entities safely without touching them.
- Improved entity safety checks (`PrefabName`, `net`, `ID`, etc.).
- Timer now properly revalidates entities before accessing containers.

### Changed
- Loot is now only multiplied on **newly spawned** wagons.
- No duplication or repopulation of existing wagons.
- Clean plugin initialization without errors or warnings.

---
## 1.0.1
- Removed `ProcessExistingWagons()` to prevent duplication of resources on plugin reload
- Multipliers now only apply to newly spawned wagons
  
---
## 1.0
- Initial release
- Multiplies ore and fuel in train wagons
- Fuel crates matched using proximity and contents
- Configurable multipliers and logging
