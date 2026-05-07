---
name: game-mechanics
description: Documents the core gameplay mechanics of 4 Blocks v2 — block sliding logic, processing order, win conditions, undo system, and input handling. Use this skill when modifying gameplay behavior or debugging game logic.
---

# Game Mechanics Reference

## Core Mechanic: Simultaneous Block Sliding
- Player inputs a direction (Up/Down/Left/Right)
- ALL 4 blocks slide simultaneously in that direction
- Each block slides until it hits a wall or another stopped block
- Blocks are processed in priority order: the block closest to the destination wall is calculated first

## Block Processing Order
When sliding in a direction, blocks are sorted so the one nearest to the wall in that direction is processed first:
- **Sliding RIGHT**: highest x-position block processed first
- **Sliding LEFT**: lowest x-position block processed first
- **Sliding UP**: highest y-position block processed first
- **Sliding DOWN**: lowest y-position block processed first

This ensures blocks correctly stack against each other.

## Win Condition
Two modes based on `LevelData.requireExactTargetMatch`:
- **Non-exact (default)**: Any block on any target position → win
- **Exact**: Block[0] must be on Target[0], Block[1] on Target[1], etc.

All 4 blocks must be on target positions simultaneously.

## Undo System
- Before each move, a snapshot of all block positions (`Vector2Int[]`) is pushed to a stack
- Undo pops the last snapshot and teleports all blocks back
- Move count decrements on undo
- Cannot undo while blocks are sliding

## Game States
```
Playing → accepts input, blocks can slide
Won     → level complete, win screen shown, no input accepted
Paused  → frozen, no input accepted
```

## Input Methods
| Platform | Method | Handler |
|---|---|---|
| Keyboard | WASD / Arrow keys | PlayerInputActions → OnMoveInput |
| Keyboard | Z = Undo, R = Restart | PlayerInputActions → OnUndoInput/OnRestartInput |
| Mobile | Swipe gestures | SwipeDetector → OnSwipeInput |
| UI | Buttons (Restart, Next, Menu) | Button onClick → GameManager methods |
