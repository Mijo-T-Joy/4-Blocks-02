using UnityEngine;

/// <summary>
/// Manages all audio playback in the game (Music and Sound Effects).
/// Persistent across scenes.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("The AudioSource component used for playing background music (should loop).")]
    public AudioSource bgmSource;
    [Tooltip("The AudioSource component used for playing sound effects.")]
    public AudioSource sfxSource;

    [Header("Audio Clips")]
    [Tooltip("Background Music clip.")]
    public AudioClip bgmClip;
    [Tooltip("Sound played when a block starts sliding.")]
    public AudioClip slideClip;
    [Tooltip("Sound played when a block hits a wall or another block and stops.")]
    public AudioClip hitClip;
    [Tooltip("Sound played when the level is completed.")]
    public AudioClip winClip;
    [Tooltip("Generic UI click sound (optional).")]
    public AudioClip clickClip;

    [Header("Settings")]
    [Tooltip("Minimum time (in seconds) between hit sounds to prevent deafening the player when 4 blocks hit at exactly the same time.")]
    public float hitSoundCooldown = 0.05f;
    private float lastHitTime = 0f;

    private float masterMusicVolume = 1f;
    private float masterSFXVolume = 1f;

    void Awake()
    {
        // Singleton pattern with DontDestroyOnLoad
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // --- DEBUG & FIX FOR MISSING SOUND ---
        
        // 1. Force global audio volume to max
        AudioListener.volume = 1f;

        // 2. Check if an AudioListener exists in the scene
        AudioListener listener = FindFirstObjectByType<AudioListener>();
        if (listener == null)
        {
            Debug.LogError("🚨 CRITICAL: NO AUDIO LISTENER FOUND IN THE SCENE! You must add an 'Audio Listener' component to your Main Camera to hear sounds! 🚨");
        }
        else 
        {
            Debug.Log("AudioManager: Found AudioListener on -> " + listener.gameObject.name);
        }

        // Auto-fetch AudioSources if not manually assigned
        AudioSource[] sources = GetComponents<AudioSource>();
        if (bgmSource == null && sources.Length > 0) bgmSource = sources[0];
        if (sfxSource == null && sources.Length > 1) sfxSource = sources[1];

        if (bgmSource != null) bgmSource.enabled = true;
        if (sfxSource != null) sfxSource.enabled = true;

        if (bgmSource == null) Debug.LogWarning("AudioManager: BGM AudioSource is missing! Please add an AudioSource component.");
        if (sfxSource == null) Debug.LogWarning("AudioManager: SFX AudioSource is missing! Please add a second AudioSource component.");

        // Load saved volume settings
        masterMusicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        masterSFXVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        
        if (bgmSource != null) bgmSource.volume = masterMusicVolume;
        if (sfxSource != null) sfxSource.volume = masterSFXVolume;
    }

    public void SetMusicVolume(float volume)
    {
        masterMusicVolume = Mathf.Clamp01(volume);
        if (bgmSource != null) bgmSource.volume = masterMusicVolume;
        PlayerPrefs.SetFloat("MusicVolume", masterMusicVolume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        masterSFXVolume = Mathf.Clamp01(volume);
        if (sfxSource != null) sfxSource.volume = masterSFXVolume;
        PlayerPrefs.SetFloat("SFXVolume", masterSFXVolume);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Starts playing the Background Music if it's not already playing.
    /// </summary>
    public void PlayBGM()
    {
        if (bgmSource == null) {
            Debug.LogWarning("AudioManager: Cannot play BGM. bgmSource is null.");
            return;
        }
        if (bgmClip == null) {
            Debug.LogWarning("AudioManager: Cannot play BGM. bgmClip is not assigned in the inspector!");
            return;
        }
        
        if (!bgmSource.isPlaying)
        {
            bgmSource.spatialBlend = 0f; // Force 2D sound
            bgmSource.volume = masterMusicVolume;
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.Play();
            Debug.Log("AudioManager: Playing BGM.");
        }
    }

    /// <summary>
    /// Plays a sound effect once.
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null) {
            Debug.LogWarning("AudioManager: Cannot play SFX. sfxSource is null.");
            return;
        }
        if (clip == null) {
            Debug.LogWarning("AudioManager: Cannot play SFX. Clip is missing/not assigned!");
            return;
        }
        sfxSource.spatialBlend = 0f; // Force 2D sound
        sfxSource.volume = masterSFXVolume;
        sfxSource.PlayOneShot(clip);
        Debug.Log("AudioManager: Played SFX -> " + clip.name);
    }

    public void PlaySlideSound()
    {
        PlaySFX(slideClip);
    }

    public void PlayHitSound()
    {
        // Cooldown prevents 4 blocks stopping at exactly the same frame from overlapping sounds too loudly
        if (Time.time - lastHitTime >= hitSoundCooldown)
        {
            PlaySFX(hitClip);
            lastHitTime = Time.time;
        }
    }

    public void PlayWinSound()
    {
        PlaySFX(winClip);
    }

    public void PlayClickSound()
    {
        PlaySFX(clickClip);
    }
}
