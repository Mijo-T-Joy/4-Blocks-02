---
name: coding-conventions
description: Defines the C# coding standards, naming patterns, file organization, and Unity-specific conventions for the 4 Blocks v2 project. Use this skill when writing or modifying any C# script to ensure consistency.
---

# Coding Conventions

## Language & Framework
- C# with Unity MonoBehaviour / ScriptableObject patterns
- Unity New Input System (NOT the legacy Input.GetKey style)

## Script Organization
- Place scripts in the appropriate subfolder under `Assets/Scripts/`:
  - `Core/` — Singletons, managers, and foundational systems (GameManager, GridSystem)
  - `Data/` — ScriptableObjects and data containers (LevelData, LevelRegistry)
  - `Gameplay/` — In-game entity behavior (Block, LevelManager)
  - `Input/` — Input handling (SwipeDetector, generated PlayerInputActions)
  - `UI/` — All UI controllers (HUD, WinScreen, LevelSelect, SafeArea)

## Naming Patterns
- **Classes**: PascalCase (e.g., `GameManager`, `LevelData`)
- **Public fields**: camelCase with `[Tooltip]` and `[Header]` attributes for Inspector
- **Private fields**: camelCase (e.g., `moveCount`, `slidingBlockCount`)
- **Properties**: PascalCase (e.g., `CurrentState`, `GridPosition`)
- **Events/Callbacks**: `On` prefix (e.g., `OnStopped`, `OnSwipe`)

## Inspector References
- Use `[Header("Section")]` to group related fields
- Use `[Tooltip("Description")]` on every serialized field
- Use `[SerializeField]` for private fields that need Inspector exposure
- Public fields are acceptable for Inspector assignment (this project uses them)

## Documentation Style
- Use XML `<summary>` comments on all public classes and methods
- Use section divider comments for logical groupings:
  ```csharp
  // ──────────────────────────────────────────────
  //  SECTION NAME
  // ──────────────────────────────────────────────
  ```

## Singleton Pattern
- GameManager uses a simple singleton: `public static GameManager Instance { get; private set; }`
- Set in `Awake()`, destroy duplicates
- Do NOT use `DontDestroyOnLoad` — GameManager is per-scene

## Event Pattern
- Use C# `Action` delegates for events (e.g., `public event Action OnStopped`)
- Subscribe in `OnEnable`, unsubscribe in `OnDisable`
- Always null-check before invoking

## Data Pattern
- Level data is stored as ScriptableObject assets created via `[CreateAssetMenu]`
- Menu path convention: `4Blocks/` (e.g., `4Blocks/Level Data`, `4Blocks/Level Registry`)
- Level assets live in `Assets/Scripts/Data/Levels/`

## Input Handling
- Keyboard/gamepad: via PlayerInputActions (New Input System)
- Touch/mobile: via SwipeDetector with `OnSwipe` event
- Both feed into the same game logic methods
- Always check `GameState` before processing input

## Grid Coordinates
- Positions use `Vector2Int` for grid coordinates
- `GridSystem` handles grid-to-world and world-to-grid conversion
- Block sliding is calculated per-block in priority order (nearest to wall first)
