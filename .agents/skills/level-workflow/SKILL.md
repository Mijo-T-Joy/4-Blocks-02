---
name: level-workflow
description: Documents how to create, configure, and register new puzzle levels in 4 Blocks v2. Use this skill when adding levels, modifying level data, or working with the LevelData/LevelRegistry ScriptableObjects.
---

# Level Creation & Management Workflow

## How to Add a New Level

1. **Create a LevelData asset**:
   - In Unity: Right-click in `Assets/Scripts/Data/Levels/` → Create → 4Blocks → Level Data
   - Name it following the pattern: `Level_XX` (e.g., `Level_05`)

2. **Configure the LevelData**:
   - Set `levelName` (displayed in HUD, e.g., "Level 05")
   - Set `gridWidth` and `gridHeight` (play area dimensions)
   - Set 4 `blockStartPositions` (Vector2Int — where blocks spawn)
   - Set 4 `blockTargetPositions` (Vector2Int — win condition positions)
   - Optionally add `wallPositions` (interior walls; boundary walls are auto-generated)
   - Set `requireExactTargetMatch`:
     - `false` = any block on any target (targets shown in gray)
     - `true` = Block[i] must reach Target[i] (targets shown in block's color)

3. **Register the level**:
   - Open the `LevelRegistry` ScriptableObject asset
   - Add the new LevelData to the `levels` array at the desired position
   - Index in the array = level number (index 0 = first level)

4. **Level Select auto-updates**:
   - `LevelSelectController` reads `LevelRegistry` and generates buttons dynamically
   - No manual UI changes needed

## Scene Flow
```
LevelSelect → (user picks level) → sets LevelSelectController.SelectedLevelIndex → loads GamePlay scene
GamePlay → GameManager reads SelectedLevelIndex → loads level from LevelRegistry
Win → LevelProgressManager.CompleteLevel() saves progress → Next Level or Back to LevelSelect
```

## Level Progress System
- Uses `PlayerPrefs` with key `"MaxCompletedLevel"`
- `LevelProgressManager.CompleteLevel(index)` — marks level as complete
- `LevelProgressManager.GetMaxCompletedLevel()` — returns highest completed level index
- Levels unlock sequentially: level N+1 unlocks after completing level N

## Prefabs Used Per Level
| Prefab | Purpose |
|---|---|
| `Block.prefab` | Sliding puzzle block (4 per level) |
| `Wall.prefab` | Wall segment (boundary + interior) |
| `Target.prefab` | Target position indicator |
