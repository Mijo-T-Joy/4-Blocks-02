using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Safe Edit-Mode sprite preview controller for validating board, block, dot, star,
/// wall cross, and border sprites without entering Play Mode.
///
/// SAFETY GUARANTEES:
///   - All preview objects are tagged HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
///     so they can NEVER be saved into scene files or included in builds.
///   - Preview objects are automatically cleared when entering Play Mode, saving a scene,
///     or closing a scene.
/// </summary>
[InitializeOnLoad]
public static class LevelSpritePreviewController
{
    private const string DefaultRegistryPath = "Assets/Scripts/Data/Levels/MainLevelRegistry.asset";
    private const string GamePlayScenePath = "Assets/Scenes/GamePlay.unity";

    public static LevelData ActivePreviewLevel { get; private set; }
    public static bool IsPreviewActive => ActivePreviewLevel != null;

    static LevelSpritePreviewController()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneClosing += OnSceneClosing;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            ClearAllPreviews();
        }
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        ClearAllPreviews();
    }

    private static void OnSceneClosing(Scene scene, bool removingScene)
    {
        ClearAllPreviews();
    }

    /// <summary>
    /// Spawns a non-saved Edit-Mode preview of the given LevelData using the scene's LevelManager.
    /// </summary>
    public static bool ShowPreview(LevelData level, LevelManager explicitLevelManager = null)
    {
        if (Application.isPlaying || level == null) return false;

        LevelManager lm = explicitLevelManager != null
            ? explicitLevelManager
            : Object.FindFirstObjectByType<LevelManager>();

        if (lm == null) return false;

        GridSystem grid = Object.FindFirstObjectByType<GridSystem>();
        if (grid == null)
        {
            Debug.LogWarning("[LevelSpritePreview] No GridSystem found in the active scene.");
            return false;
        }

        grid.SetupGrid(level);
        lm.LoadLevel(level, grid);

        // Tag every spawned child under LevelManager as DontSave so it never pollutes scenes or builds
        HideFlags previewFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        for (int i = 0; i < lm.transform.childCount; i++)
        {
            SetHideFlagsRecursive(lm.transform.GetChild(i).gameObject, previewFlags);
        }

        ActivePreviewLevel = level;
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
        return true;
    }

    /// <summary>
    /// Clears all Edit-Mode preview objects cleanly.
    /// </summary>
    public static void ClearAllPreviews()
    {
        if (Application.isPlaying)
        {
            ActivePreviewLevel = null;
            return;
        }

        LevelManager[] managers = Object.FindObjectsByType<LevelManager>(FindObjectsSortMode.None);
        foreach (LevelManager lm in managers)
        {
            if (lm != null)
            {
                lm.ClearLevel();
            }
        }

        ActivePreviewLevel = null;
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Returns all valid LevelData assets registered in MainLevelRegistry (or scene GameManager).
    /// </summary>
    public static List<LevelData> GetRegisteredLevels()
    {
        List<LevelData> result = new List<LevelData>();

        LevelRegistry registry = null;
        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null && gm.levelRegistry != null)
        {
            registry = gm.levelRegistry;
        }

        if (registry == null)
        {
            registry = AssetDatabase.LoadAssetAtPath<LevelRegistry>(DefaultRegistryPath);
        }

        if (registry != null && registry.levels != null)
        {
            foreach (LevelData lvl in registry.levels)
            {
                if (lvl != null) result.Add(lvl);
            }
        }

        return result;
    }

    public static void OpenGamePlayScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(GamePlayScenePath, OpenSceneMode.Single);
        }
    }

    private static void SetHideFlagsRecursive(GameObject go, HideFlags flags)
    {
        if (go == null) return;
        go.hideFlags = flags;
        for (int i = 0; i < go.transform.childCount; i++)
        {
            SetHideFlagsRecursive(go.transform.GetChild(i).gameObject, flags);
        }
    }
}

/// <summary>
/// Custom Inspector on LevelManager adding an Edit-Mode Level Sprite Preview selector
/// with live auto-refresh when sprites, colors, or border properties change.
/// </summary>
[CustomEditor(typeof(LevelManager))]
public class LevelManagerPreviewEditor : Editor
{
    private int selectedIndex;
    private LevelData customLevel;
    private bool autoRefresh = true;

    public override void OnInspectorGUI()
    {
        LevelManager lm = (LevelManager)target;

        DrawPreviewBox(lm);

        EditorGUILayout.Space(8);

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool inspectorChanged = EditorGUI.EndChangeCheck();

        if (inspectorChanged && !Application.isPlaying && autoRefresh && LevelSpritePreviewController.IsPreviewActive)
        {
            LevelSpritePreviewController.ShowPreview(LevelSpritePreviewController.ActivePreviewLevel, lm);
        }
    }

    private void DrawPreviewBox(LevelManager lm)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Level Sprite Preview (No Play Mode Needed)", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Preview selector is available in Edit Mode. Exit Play Mode to preview levels here.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        List<LevelData> levels = LevelSpritePreviewController.GetRegisteredLevels();

        if (levels.Count > 0)
        {
            string[] names = new string[levels.Count];
            for (int i = 0; i < levels.Count; i++)
            {
                string label = string.IsNullOrEmpty(levels[i].levelName) ? levels[i].name : levels[i].levelName;
                names[i] = $"{i + 1}. {label} ({levels[i].gridWidth}x{levels[i].gridHeight})";
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, levels.Count - 1);

            EditorGUILayout.BeginHorizontal();
            int newIndex = EditorGUILayout.Popup("Select Level", selectedIndex, names);
            if (GUILayout.Button("◀", GUILayout.Width(26f))) newIndex = (selectedIndex - 1 + levels.Count) % levels.Count;
            if (GUILayout.Button("▶", GUILayout.Width(26f))) newIndex = (selectedIndex + 1) % levels.Count;
            EditorGUILayout.EndHorizontal();

            if (newIndex != selectedIndex)
            {
                selectedIndex = newIndex;
                customLevel = null;
                if (LevelSpritePreviewController.IsPreviewActive)
                {
                    LevelSpritePreviewController.ShowPreview(levels[selectedIndex], lm);
                }
            }
        }

        LevelData pickedCustom = (LevelData)EditorGUILayout.ObjectField("Or Custom Level", customLevel, typeof(LevelData), false);
        if (pickedCustom != customLevel)
        {
            customLevel = pickedCustom;
            if (customLevel != null && LevelSpritePreviewController.IsPreviewActive)
            {
                LevelSpritePreviewController.ShowPreview(customLevel, lm);
            }
        }

        LevelData targetLevel = customLevel != null
            ? customLevel
            : (levels.Count > 0 ? levels[selectedIndex] : null);

        autoRefresh = EditorGUILayout.Toggle("Auto-Refresh on Sprite / Color Edit", autoRefresh);

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = targetLevel != null;
        string showLabel = LevelSpritePreviewController.IsPreviewActive ? "Refresh Preview" : "Show Level Preview";
        if (GUILayout.Button(showLabel, GUILayout.Height(24f)))
        {
            LevelSpritePreviewController.ShowPreview(targetLevel, lm);
        }
        GUI.enabled = true;

        GUI.enabled = LevelSpritePreviewController.IsPreviewActive;
        if (GUILayout.Button("Clear Preview", GUILayout.Height(24f), GUILayout.Width(105f)))
        {
            LevelSpritePreviewController.ClearAllPreviews();
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        if (LevelSpritePreviewController.IsPreviewActive)
        {
            EditorGUILayout.HelpBox(
                $"Previewing: {LevelSpritePreviewController.ActivePreviewLevel.name} (not saved to scene; auto-clears on Play or Save).",
                MessageType.Info
            );
        }

        EditorGUILayout.EndVertical();
    }
}

/// <summary>
/// Dockable Editor window for switching level previews while editing PNG assets in the Project window.
/// Open via: Tools -> 4Blocks -> Level Sprite Preview
/// </summary>
public class LevelSpritePreviewWindow : EditorWindow
{
    private int selectedIndex;
    private LevelData customLevel;

    [MenuItem("Tools/4Blocks/Level Sprite Preview")]
    public static void OpenWindow()
    {
        LevelSpritePreviewWindow win = GetWindow<LevelSpritePreviewWindow>("Level Sprite Preview");
        win.minSize = new Vector2(300f, 190f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Edit-Mode Sprite Validator", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Exit Play Mode to use the Edit-Mode Level Sprite Preview.", MessageType.Info);
            return;
        }

        LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
        if (lm == null)
        {
            EditorGUILayout.HelpBox("No LevelManager in the current scene. Open GamePlay.unity to preview level sprites.", MessageType.Warning);
            if (GUILayout.Button("Open GamePlay Scene", GUILayout.Height(26f)))
            {
                LevelSpritePreviewController.OpenGamePlayScene();
            }
            return;
        }

        List<LevelData> levels = LevelSpritePreviewController.GetRegisteredLevels();
        if (levels.Count > 0)
        {
            string[] names = new string[levels.Count];
            for (int i = 0; i < levels.Count; i++)
            {
                string label = string.IsNullOrEmpty(levels[i].levelName) ? levels[i].name : levels[i].levelName;
                names[i] = $"{i + 1}. {label} ({levels[i].gridWidth}x{levels[i].gridHeight})";
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, levels.Count - 1);

            EditorGUILayout.BeginHorizontal();
            int newIndex = EditorGUILayout.Popup("Level", selectedIndex, names);
            if (GUILayout.Button("◀", GUILayout.Width(26f))) newIndex = (selectedIndex - 1 + levels.Count) % levels.Count;
            if (GUILayout.Button("▶", GUILayout.Width(26f))) newIndex = (selectedIndex + 1) % levels.Count;
            EditorGUILayout.EndHorizontal();

            if (newIndex != selectedIndex)
            {
                selectedIndex = newIndex;
                customLevel = null;
                LevelSpritePreviewController.ShowPreview(levels[selectedIndex], lm);
            }
        }

        customLevel = (LevelData)EditorGUILayout.ObjectField("Custom Level", customLevel, typeof(LevelData), false);

        LevelData targetLevel = customLevel != null
            ? customLevel
            : (levels.Count > 0 ? levels[selectedIndex] : null);

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = targetLevel != null;
        if (GUILayout.Button("Show / Refresh Preview", GUILayout.Height(26f)))
        {
            LevelSpritePreviewController.ShowPreview(targetLevel, lm);
        }
        GUI.enabled = true;

        if (GUILayout.Button("Clear Preview", GUILayout.Height(26f), GUILayout.Width(105f)))
        {
            LevelSpritePreviewController.ClearAllPreviews();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Select LevelManager in Inspector", GUILayout.Height(22f)))
        {
            Selection.activeGameObject = lm.gameObject;
            EditorGUIUtility.PingObject(lm.gameObject);
        }

        if (LevelSpritePreviewController.IsPreviewActive)
        {
            EditorGUILayout.HelpBox(
                $"Active Preview: {LevelSpritePreviewController.ActivePreviewLevel.name}\n" +
                "Safe: marked DontSave & auto-clears on Play or Scene Save.",
                MessageType.Info
            );
        }
    }
}

