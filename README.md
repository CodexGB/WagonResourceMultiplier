# WagonResourceMultiplier
**Version:** 1.0  
**Author:** Codex

## Description
Multiplies ore and fuel amounts in train wagons.  
Supports ore wagons and fuel wagons by prefab name and proximity.

## Features
- Configurable multipliers for ore and fuel wagons
- Works on newly spawned wagons and those present at server start
- Fuel crates matched using proximity + content detection
- Lightweight and automatic on server load

> **Note:** Multipliers apply only to wagons that have recently spawned after the plugin was added/loaded.

## Installation
1. Place `WagonResourceMultiplier.cs` into your `oxide/plugins` folder.
2. Restart your server or run `oxide.reload WagonResourceMultiplier`.

Wagons already spawned on the map will be processed if they are active and unlooted at the time of plugin load.

## Configuration

Located in: `oxide/config/WagonResourceMultiplier.json`

```json
{
  "EnableLogging": false,
  "WagonMultipliers": {
    "Wagon Ore Multiplier": 1.0,
    "Wagon Fuel Multiplier": 1.0
  }
}
