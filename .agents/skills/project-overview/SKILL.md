---
name: project-overview
description: Provides a high-level understanding of the 4 Blocks v2 Unity puzzle game — its purpose, scene flow, folder structure, and core architecture. Use this skill when working on any part of the project to understand the overall system.
---

# 4 Blocks v2 — Project Overview

## Game Description
A 2D sliding-block puzzle game built in Unity. The player slides 4 color blocks simultaneously across a grid. All blocks move at once in the same direction and slide until hitting a wall or another block. The goal is to land all 4 blocks on their target positions.

## Target Platforms
- Android
- iOS
- Windows

## Unity Version & Orientation
- Portrait orientation
- Uses Unity New Input System

## Scenes
| Scene | Purpose |
|---|---|
| `LevelSelect` | Starting scene — grid of level buttons, locked/unlocked based on progress |
| `GamePlay` | Active puzzle gameplay — blocks, walls, targets, HUD, win screen |

## Folder Structure
```
Assets/
├── Font/              # Custom fonts
├── Images/            # Sprites and images
├── Prefabs/           # Block.prefab, Wall.prefab, Target.prefab, UI/
├── Scenes/            # GamePlay.unity, LevelSelect.unity
├── Scripts/
│   ├── Core/          # GameManager, GridSystem, CameraScaler, LevelProgressManager
│   ├── Data/          # LevelData (ScriptableObject), LevelRegistry, Levels/
│   ├── Gameplay/      # Block, LevelManager
│   ├── Input/         # PlayerInputActions, SwipeDetector
│   └── UI/            # HUDController, WinScreenController, LevelSelectController, SafeAreaHandler
├── Settings/          # Input action settings
└── TextMesh Pro/      # TMP assets
```

## Core Architecture
- **GameManager** (Singleton) — Central controller. Reads input → slides all blocks → tracks moves → checks win condition → manages undo stack
- **GridSystem** — Manages the grid coordinates and calculates slide destinations
- **LevelManager** — Reads LevelData and spawns walls, blocks, and targets
- **LevelData** (ScriptableObject) — Pure data for a single level: grid size, block start/target positions, wall positions
- **LevelRegistry** (ScriptableObject) — Ordered array of all LevelData assets
- **LevelProgressManager** — Static utility that saves/loads level progress via PlayerPrefs
- **Block** — Individual block behavior: sliding animation, grid position tracking
