using UnityEngine;
using TMPro;

/// <summary>
/// Controls the Heads-Up Display (HUD) shown during gameplay.
///
/// WHAT THIS DOES:
///   Updates the on-screen text for:
///   - Current Move Count
///   - Current Level Name
///
/// SETUP IN UNITY:
///   1. Create a TextMeshPro object in your Canvas for the Moves (e.g., "Moves: 0")
///   2. Create a TextMeshPro object in your Canvas for the Level Name
///   3. Attach this script to the Canvas or a HUD Panel
///   4. Drag the TMP text objects into the public slots in the Inspector
///   5. Drag this HUDController component into the GameManager's "HUD Controller" slot
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("TextMeshPro text element that displays the move count")]
    public TextMeshProUGUI moveCountText;

    [Tooltip("TextMeshPro text element that displays the level name")]
    public TextMeshProUGUI levelNameText;

    /// <summary>
    /// Updates the move counter on screen.
    /// Called by GameManager whenever a move finishes or is undone.
    /// </summary>
    public void UpdateMoveCount(int count)
    {
        if (moveCountText != null)
        {
            moveCountText.text = $"Moves: {count}";
        }
    }

    /// <summary>
    /// Updates the level name on screen.
    /// Called by GameManager when a new level loads.
    /// </summary>
    public void UpdateLevelName(string name)
    {
        if (levelNameText != null)
        {
            levelNameText.text = name;
        }
    }
}
