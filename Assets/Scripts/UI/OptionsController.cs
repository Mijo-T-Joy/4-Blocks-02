using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles UI logic for the Options Menu (Volume Sliders & Reset Progress).
/// </summary>
public class OptionsController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Slider for controlling background music volume (Min 0, Max 1)")]
    public Slider musicSlider;
    
    [Tooltip("Slider for controlling sound effects volume (Min 0, Max 1)")]
    public Slider sfxSlider;

    [Tooltip("Drag the Options Panel GameObject here so the script can hide it")]
    public GameObject optionsPanel;

    void Start()
    {
        // Initialize sliders with saved values from PlayerPrefs
        if (musicSlider != null)
        {
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        // Make sure the panel is hidden when the game starts so it doesn't overlap the level select
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Called automatically when the Music Slider value changes.
    /// </summary>
    public void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }
    }

    /// <summary>
    /// Called automatically when the SFX Slider value changes.
    /// </summary>
    public void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }
    }

    /// <summary>
    /// Wipes player progress and reloads the scene. 
    /// Link this to the "Reset Progress" Button OnClick() event.
    /// </summary>
    public void ResetProgress()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClickSound();
        }

        // Wipe data
        LevelProgressManager.ResetAllProgress();
        Debug.Log("OptionsController: Player Progress Reset!");

        // Reload the current scene to update any locked UI elements
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Opens the options menu. Link this to an "Options" button on your main menu.
    /// </summary>
    public void OpenOptions()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClickSound();
        }

        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
        }
        else
        {
            // Fallback
            gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Closes the options menu. Link this to a "Back" or "X" button.
    /// </summary>
    public void CloseOptions()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClickSound();
        }

        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
        }
        else
        {
            // Fallback: If they attached this script directly to the panel and forgot to assign the reference
            gameObject.SetActive(false);
        }
    }
}
