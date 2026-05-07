using UnityEngine;

/// <summary>
/// Static helper that persists level completion using PlayerPrefs.
///
/// KEY FORMAT:
///   "LevelComplete_0" = 1   → Level 0 (first level) is completed
///   "HighestUnlocked" = 2   → Levels 0, 1, 2 are playable
///
/// Level 0 is always unlocked. Completing level N unlocks level N+1.
/// </summary>
public static class LevelProgressManager
{
    private const string COMPLETED_KEY = "LevelComplete_";
    private const string HIGHEST_KEY = "HighestUnlocked";

    /// <summary>
    /// Returns the highest level index the player can access.
    /// Level 0 is always unlocked.
    /// </summary>
    public static int GetHighestUnlockedLevel()
    {
        return PlayerPrefs.GetInt(HIGHEST_KEY, 0);
    }

    /// <summary>
    /// Returns true if the given level index is playable.
    /// </summary>
    public static bool IsLevelUnlocked(int levelIndex)
    {
        return levelIndex <= GetHighestUnlockedLevel();
    }

    /// <summary>
    /// Returns true if the player has already completed this level.
    /// </summary>
    public static bool IsLevelCompleted(int levelIndex)
    {
        return PlayerPrefs.GetInt(COMPLETED_KEY + levelIndex, 0) == 1;
    }

    /// <summary>
    /// Marks a level as completed and unlocks the next one.
    /// Call this when the player wins a level.
    /// </summary>
    public static void CompleteLevel(int levelIndex)
    {
        // Mark this level as completed
        PlayerPrefs.SetInt(COMPLETED_KEY + levelIndex, 1);

        // Unlock the next level if it hasn't been unlocked yet
        int currentHighest = GetHighestUnlockedLevel();
        if (levelIndex >= currentHighest)
        {
            PlayerPrefs.SetInt(HIGHEST_KEY, levelIndex + 1);
        }

        PlayerPrefs.Save();
    }

    /// <summary>
    /// Wipes all saved progress. Useful for testing or a "Reset" button.
    /// </summary>
    public static void ResetAllProgress()
    {
        // We don't know how many levels exist, so just clear a generous range
        for (int i = 0; i < 200; i++)
        {
            PlayerPrefs.DeleteKey(COMPLETED_KEY + i);
        }
        PlayerPrefs.DeleteKey(HIGHEST_KEY);
        PlayerPrefs.Save();
    }
}
