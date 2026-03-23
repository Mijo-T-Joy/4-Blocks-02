using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Controls the Win Screen overlay that appears when a level is completed.
///
/// WHAT THIS DOES:
///   - Shows the final move count
///   - Provides buttons to Restart or go to the Next Level
///
/// SETUP IN UNITY:
///   1. Create a Panel inside your Canvas to be the Win Screen
///   2. Add TextMeshPro text for "Level Complete!" and "Moves: X"
///   3. Add 2 Buttons (Restart, Next Level)
///   4. Attach this script to the Panel
///   5. Drag the Moves Text into the inspector slot
///   6. Disable the Panel GameObject by default (GameManager will enable it)
///   7. On your Buttons, set the OnClick() events to call:
///      - WinScreenController.OnRestartClicked()
///      - WinScreenController.OnNextLevelClicked()
///   8. Finally, drag this Panel into GameManager's "Win Screen Controller" slot
/// </summary>
public class WinScreenController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("TextMeshPro text element showing the final move count")]
    public TextMeshProUGUI moveCountText;

    /// <summary>
    /// Activates the Win Screen and displays the final score.
    /// Called by GameManager when all blocks reach their targets.
    /// </summary>
    public void Show(int finalMoveCount)
    {
        gameObject.SetActive(true);

        if (moveCountText != null)
        {
            moveCountText.text = $"Completed in {finalMoveCount} moves!";
        }
    }

    /// <summary>
    /// Deactivates the Win Screen.
    /// Called by GameManager when a level starts or restarts.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // ──────────────────────────────────────────────
    //  BUTTON CALLBACKS
    // ──────────────────────────────────────────────

    /// <summary>
    /// Link this to the "Restart" Button's OnClick event in the Inspector.
    /// </summary>
    public void OnRestartClicked()
    {
        GameManager.Instance.RestartLevel();
    }

    /// <summary>
    /// Link this to the "Next Level" Button's OnClick event in the Inspector.
    /// </summary>
    public void OnNextLevelClicked()
    {
        GameManager.Instance.LoadNextLevel();
    }
}
