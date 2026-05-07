using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Candy Crush / Angry Birds style level select screen.
///
/// Dynamically creates numbered level buttons in a grid.
///   - UNLOCKED + COMPLETED  → colored button with "✓" + number
///   - UNLOCKED + NOT PLAYED → colored button with just the number
///   - LOCKED                → grayed out, non-interactable, shows "🔒"
///
/// SETUP IN UNITY:
///   1. Create a new scene called "LevelSelect"
///   2. Add a Canvas (Screen Space - Overlay, Scale With Screen Size 1080×1920)
///   3. Add a Panel with a GridLayoutGroup as the button container
///   4. Create a LevelButton prefab (Button + child TextMeshProUGUI)
///   5. Attach this script to an empty GameObject in the scene
///   6. Assign: levelRegistry, levelButtonPrefab, buttonParent
///   7. Add this scene as index 0 in Build Settings
/// </summary>
public class LevelSelectController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The master level registry asset")]
    public LevelRegistry levelRegistry;

    [Tooltip("Button prefab with a TextMeshProUGUI child for the label")]
    public GameObject levelButtonPrefab;

    [Tooltip("Parent transform with a GridLayoutGroup to hold the buttons")]
    public Transform buttonParent;

    [Header("Colors")]
    [Tooltip("Color for unlocked, playable levels")]
    public Color unlockedColor = new Color(0.2f, 0.6f, 1f, 1f);    // Blue

    [Tooltip("Color for completed levels")]
    public Color completedColor = new Color(0.2f, 0.8f, 0.3f, 1f);  // Green

    [Tooltip("Color for locked levels")]
    public Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);   // Gray

    [Header("Scene")]
    [Tooltip("Name of the gameplay scene to load")]
    public string playSceneName = "GamePlay";

    /// <summary>
    /// Static field read by GameManager to know which level to load.
    /// Set right before loading the play scene.
    /// </summary>
    public static int SelectedLevelIndex = 0;

    void Start()
    {
        BuildLevelButtons();
    }

    /// <summary>
    /// Creates one button per level in the registry.
    /// </summary>
    private void BuildLevelButtons()
    {
        if (levelRegistry == null || levelRegistry.levels == null)
        {
            Debug.LogError("LevelSelectController: LevelRegistry is not assigned!");
            return;
        }

        // Clear any existing buttons (in case of re-entry)
        foreach (Transform child in buttonParent)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < levelRegistry.levels.Length; i++)
        {
            int levelIndex = i; // Capture for lambda

            // Instantiate button
            GameObject buttonObj = Instantiate(levelButtonPrefab, buttonParent);
            buttonObj.name = $"LevelBtn_{levelIndex + 1}";

            Button button = buttonObj.GetComponent<Button>();
            TextMeshProUGUI label = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            Image bgImage = buttonObj.GetComponent<Image>();

            bool isUnlocked = LevelProgressManager.IsLevelUnlocked(levelIndex);
            bool isCompleted = LevelProgressManager.IsLevelCompleted(levelIndex);

            if (isUnlocked)
            {
                if (isCompleted)
                {
                    // ✓ Completed — green with checkmark
                    if (label != null) label.text = $"{levelIndex + 1}\nDone";
                    if (bgImage != null) bgImage.color = completedColor;
                }
                else
                {
                    // Unlocked but not completed — blue with number
                    if (label != null) label.text = $"{levelIndex + 1}";
                    if (bgImage != null) bgImage.color = unlockedColor;
                }

                // Wire up click
                button.interactable = true;
                button.onClick.AddListener(() => OnLevelClicked(levelIndex));
            }
            else
            {
                // 🔒 Locked — gray, non-interactable
                if (label != null) label.text = $"{levelIndex + 1}\nLocked";
                if (bgImage != null) bgImage.color = lockedColor;
                button.interactable = false;
            }
        }
    }

    /// <summary>
    /// Called when a level button is tapped. Stores the index and loads the play scene.
    /// </summary>
    private void OnLevelClicked(int levelIndex)
    {
        SelectedLevelIndex = levelIndex;
        SceneManager.LoadScene(playSceneName);
    }
}
