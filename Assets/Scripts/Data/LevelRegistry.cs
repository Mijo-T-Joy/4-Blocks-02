using UnityEngine;

/// <summary>
/// Master list of all levels in order.
///
/// Both the Level Select scene and the Play scene reference this single
/// asset so the level order is always consistent.
///
/// CREATE IN UNITY:
///   Right-click in Project → Create → 4Blocks → Level Registry
///   Then drag Level_01, Level_02, Level_03, etc. into the levels array.
/// </summary>
[CreateAssetMenu(fileName = "LevelRegistry", menuName = "4Blocks/Level Registry")]
public class LevelRegistry : ScriptableObject
{
    [Tooltip("All levels in play order. Index 0 = first level the player sees.")]
    public LevelData[] levels;
}
